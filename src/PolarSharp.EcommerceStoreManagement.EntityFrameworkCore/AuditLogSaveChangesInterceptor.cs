using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;

/// <summary>
/// EF Core <see cref="ISaveChangesInterceptor"/> that captures every mutation against a
/// tenant-owned catalog entity into an <see cref="AdminAuditLogEntry"/> row in the same
/// transaction. Registered automatically by <c>AddPolarCatalogServices</c>; wired into
/// each provider's <c>UseXxxCatalog</c> extension so it fires on every <c>SaveChanges</c>
/// against the catalog DbContext.
/// </summary>
/// <remarks>
/// <para>
/// Inspects <see cref="ChangeTracker.Entries"/> for <see cref="ITenantOwned"/> entities
/// (excluding <see cref="AdminAuditLogEntry"/> itself to prevent recursion) at
/// <see cref="SavingChangesAsync"/> time, and adds one audit row per mutated entity to the
/// same change-tracker before the underlying SaveChanges runs. Before/after values are
/// captured via the entity's <see cref="EntityEntry.OriginalValues"/> /
/// <see cref="EntityEntry.CurrentValues"/> and serialized to JSON for the audit row.
/// </para>
/// <para>
/// <strong>Cross-tenant marker.</strong> When the resolved <see cref="AuditActor"/>'s
/// <see cref="AuditActor.CurrentTenantId"/> differs from the mutated entity's
/// <c>TenantId</c>, <see cref="AdminAuditLogEntry.CrossTenantAccess"/> is set to true. The
/// per-tenant audit row lands here in the same transaction; the corresponding
/// platform-wide <c>PlatformAuditLogEntry</c> dual-write (defined in
/// <c>PolarSharp.MultiTenant.Identity</c>) lives in a different DbContext and is owned by
/// that package's own interceptor wiring — out of scope for this interceptor.
/// </para>
/// <para>
/// <strong>What it does NOT capture:</strong> mutations to <see cref="AdminAuditLogEntry"/>
/// itself (recursion guard) and mutations to entities that don't implement
/// <see cref="ITenantOwned"/>. Manual <see cref="AdminAuditLogEntry"/> writes (e.g. the
/// structured row <c>RefundService</c> emits with action-specific
/// <see cref="AdminAuditLogEntry.ChangedFields"/>) coexist with this interceptor — the
/// interceptor is additive, capturing the generic CRUD layer that explicit writes don't.
/// </para>
/// <para>
/// <strong>Performance note:</strong> on bulk inserts (e.g. <c>PolarSharp.DataSeeding</c>
/// generating thousands of fake products) one audit row is created per mutated entity.
/// Fake-data entries are tagged with <see cref="AdminAuditLogEntry.IsFakeData"/>=true so
/// they fall under the same global query filter that hides fake-data rows in normal
/// queries — they don't pollute the operator's audit view unless
/// <c>AllowFakeData</c> is on for the tenant.
/// </para>
/// </remarks>
public sealed class AuditLogSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IAuditLogActorProvider _actorProvider;
    private readonly TimeProvider _time;
    private readonly ILogger<AuditLogSaveChangesInterceptor> _logger;

    /// <summary>Initializes the interceptor.</summary>
    public AuditLogSaveChangesInterceptor(
        IAuditLogActorProvider actorProvider,
        TimeProvider time,
        ILogger<AuditLogSaveChangesInterceptor> logger)
    {
        _actorProvider = actorProvider ?? throw new ArgumentNullException(nameof(actorProvider));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } ctx) AddAuditEntries(ctx);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } ctx) AddAuditEntries(ctx);
        return base.SavingChanges(eventData, result);
    }

    private void AddAuditEntries(DbContext ctx)
    {
        AuditActor actor;
        try
        {
            actor = _actorProvider.GetCurrentActor();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit-log actor resolution failed; falling back to system actor for this SaveChanges.");
            actor = AuditActor.System;
        }

        var occurredAt = _time.GetUtcNow();

        // Snapshot the entries first because we'll be Adding to the change tracker mid-iteration.
        var trackedTenantOwned = ctx.ChangeTracker.Entries<ITenantOwned>().ToList();
        var auditRows = new List<AdminAuditLogEntry>(trackedTenantOwned.Count);

        foreach (var entry in trackedTenantOwned)
        {
            // Recursion guard — never audit the audit log itself.
            if (entry.Entity is AdminAuditLogEntry) continue;
            // Only Added / Modified / Deleted are interesting.
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var row = BuildAuditRow(entry, actor, occurredAt);
            if (row is not null) auditRows.Add(row);
        }

        foreach (var row in auditRows) ctx.Add(row);
    }

    private static AdminAuditLogEntry? BuildAuditRow(
        EntityEntry<ITenantOwned> entry,
        AuditActor actor,
        DateTimeOffset occurredAt)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Create,
            EntityState.Modified => AuditAction.Update,
            EntityState.Deleted => AuditAction.Delete,
            _ => (AuditAction?)null,
        };
        if (action is null) return null;

        var entityTenantId = entry.Entity.TenantId;
        // Compare as strings — tenant ids are conventionally Guids in PolarSharp but the
        // entity carries them as strings (per ITenantOwned) and custom ITenantInfo impls may
        // use non-Guid identifiers. String compare handles both cases without parser fragility.
        var crossTenant = actor.CurrentTenantId is { } current
                          && !string.Equals(current.ToString(), entityTenantId, StringComparison.OrdinalIgnoreCase);
        var isFake = entry.Entity is IFakeDataAware fake && fake.IsFakeData;

        JsonNode? before = null;
        JsonNode? after = null;
        IReadOnlyList<string> changedFields = [];

        switch (action.Value)
        {
            case AuditAction.Create:
                after = SnapshotValues(entry, isOriginal: false);
                break;
            case AuditAction.Update:
                before = SnapshotValues(entry, isOriginal: true);
                after = SnapshotValues(entry, isOriginal: false);
                changedFields = entry.Properties
                    .Where(p => p.IsModified && !p.Metadata.IsPrimaryKey())
                    .Select(p => p.Metadata.Name)
                    .ToList();
                break;
            case AuditAction.Delete:
                before = SnapshotValues(entry, isOriginal: true);
                break;
        }

        return new AdminAuditLogEntry
        {
            Id = Guid.NewGuid(),
            TenantId = entityTenantId,           // already resolved on the entity; will not be re-stamped
            ActorUserId = actor.UserId,
            ActorEmail = actor.Email,
            EntityType = entry.Entity.GetType().Name,
            EntityId = ExtractPrimaryKeyAsGuid(entry),
            Action = action.Value,
            OccurredAt = occurredAt,
            BeforeValues = before,
            AfterValues = after,
            ChangedFields = changedFields,
            IsFakeData = isFake,
            CrossTenantAccess = crossTenant,
            CrossTenantJustification = null,     // captured separately by the [AllowCrossTenant] middleware when present
        };
    }

    private static Guid ExtractPrimaryKeyAsGuid(EntityEntry entry)
    {
        var pk = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
        return pk?.CurrentValue switch
        {
            Guid g => g,
            string s when Guid.TryParse(s, out var parsed) => parsed,
            _ => Guid.Empty,
        };
    }

    private static JsonObject SnapshotValues(EntityEntry entry, bool isOriginal)
    {
        var values = isOriginal ? entry.OriginalValues : entry.CurrentValues;
        var snapshot = new JsonObject();
        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            // Skip the audit row's own JSON columns when an audit entry happens to be inspected
            // (defensive — should never be reached because of the recursion guard, but cheap).
            if (name is "BeforeValues" or "AfterValues") continue;
            var raw = values[name];
            snapshot[name] = raw switch
            {
                null => null,
                Guid g => JsonValue.Create(g.ToString()),
                DateTimeOffset dto => JsonValue.Create(dto.ToString("O")),
                DateTime dt => JsonValue.Create(dt.ToString("O")),
                bool b => JsonValue.Create(b),
                int i => JsonValue.Create(i),
                long l => JsonValue.Create(l),
                decimal d => JsonValue.Create(d),
                double db => JsonValue.Create(db),
                _ => JsonValue.Create(raw.ToString()),
            };
        }
        return snapshot;
    }
}
