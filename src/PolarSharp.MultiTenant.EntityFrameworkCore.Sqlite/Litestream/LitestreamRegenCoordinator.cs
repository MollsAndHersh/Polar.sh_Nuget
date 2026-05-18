using System.Collections.Frozen;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

/// <summary>
/// Singleton coordinator that holds the current set of tenants excluded from Litestream
/// replication, plus the regeneration-signal channel shared with the
/// <see cref="LitestreamConfigAutoRegeneratorHostedService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stage C.4 splits responsibilities across three pieces:
/// </para>
/// <list type="bullet">
///   <item>
///   <see cref="LitestreamRegenCoordinator"/> (this type) owns the exclusion set + the
///   shared signal channel and is the single source of truth for "which tenants are
///   currently excluded from replication."
///   </item>
///   <item>
///   <see cref="LitestreamConfigAutoRegeneratorHostedService"/> hosts the file-system
///   watcher and reads the coordinator's channel to drive YAML regeneration.
///   </item>
///   <item>
///   <see cref="LitestreamTenantLifecycleHandler"/> bridges MediatR
///   <c>TenantStatusChangedNotification</c> events into coordinator
///   <see cref="AddExclusion"/> / <see cref="RemoveExclusion"/> calls.
///   </item>
/// </list>
/// <para>
/// All mutator methods (<see cref="AddExclusion"/>, <see cref="RemoveExclusion"/>,
/// <see cref="SeedExclusions"/>) snapshot to an immutable <see cref="FrozenSet{T}"/> under
/// a lock and publish the new snapshot via volatile reference replacement, so the hot-path
/// reader (<see cref="GetCurrentExclusions"/>) is allocation-free and lock-free.
/// </para>
/// </remarks>
public sealed class LitestreamRegenCoordinator
{
    private readonly ILogger<LitestreamRegenCoordinator> _logger;
    private readonly Lock _mutationGate = new();
    private readonly Channel<RegenSignal> _channel;
    private FrozenSet<Guid> _excludedTenantIds = FrozenSet<Guid>.Empty;

    /// <summary>Initializes a new <see cref="LitestreamRegenCoordinator"/>.</summary>
    /// <param name="logger">Logger for diagnostic traces.</param>
    public LitestreamRegenCoordinator(ILogger<LitestreamRegenCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _channel = Channel.CreateUnbounded<RegenSignal>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    }

    /// <summary>
    /// Gets the signal channel reader used by
    /// <see cref="LitestreamConfigAutoRegeneratorHostedService"/> to receive regen triggers
    /// from both file-system events and tenant-lifecycle events.
    /// </summary>
    public ChannelReader<RegenSignal> Reader => _channel.Reader;

    /// <summary>
    /// Returns an immutable snapshot of the currently excluded tenant IDs. Allocation-free
    /// in steady state — returns the same <see cref="FrozenSet{T}"/> instance until the next
    /// mutation.
    /// </summary>
    public IReadOnlySet<Guid> GetCurrentExclusions() => _excludedTenantIds;

    /// <summary>
    /// Enqueues a regen signal without changing the exclusion set. Used by the hosted
    /// service to trigger startup-initial-sync and file-system-event-driven regens through
    /// the same channel.
    /// </summary>
    /// <param name="signal">The signal payload (used for log diagnostics).</param>
    public void SignalRegen(RegenSignal signal) => _channel.Writer.TryWrite(signal);

    /// <summary>
    /// Adds the given tenant to the exclusion set and signals a regen.
    /// </summary>
    /// <param name="tenantId">The tenant to exclude.</param>
    /// <param name="reason">Human-readable reason surfaced in logs (e.g., "Active -> Suspended: billing dispute").</param>
    /// <returns><see langword="true"/> if the tenant was newly added; <see langword="false"/> if it was already excluded.</returns>
    public bool AddExclusion(Guid tenantId, string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);

        bool added;
        lock (_mutationGate)
        {
            if (_excludedTenantIds.Contains(tenantId))
            {
                added = false;
            }
            else
            {
                var next = new HashSet<Guid>(_excludedTenantIds) { tenantId };
                _excludedTenantIds = next.ToFrozenSet();
                added = true;
            }
        }

        if (added)
        {
            _logger.LogInformation(
                "Litestream regen coordinator: tenant {TenantId} added to exclusion set ({Reason}).",
                tenantId, reason);
            _channel.Writer.TryWrite(new RegenSignal(RegenTrigger.TenantExcluded, tenantId, reason));
        }
        else
        {
            _logger.LogDebug(
                "Litestream regen coordinator: tenant {TenantId} was already excluded; no-op ({Reason}).",
                tenantId, reason);
        }

        return added;
    }

    /// <summary>
    /// Removes the given tenant from the exclusion set and signals a regen.
    /// </summary>
    /// <param name="tenantId">The tenant to re-include (typically because it was reactivated).</param>
    /// <returns><see langword="true"/> if the tenant was present and removed; <see langword="false"/> if it was not in the set.</returns>
    public bool RemoveExclusion(Guid tenantId)
    {
        bool removed;
        lock (_mutationGate)
        {
            if (!_excludedTenantIds.Contains(tenantId))
            {
                removed = false;
            }
            else
            {
                var next = new HashSet<Guid>(_excludedTenantIds);
                next.Remove(tenantId);
                _excludedTenantIds = next.ToFrozenSet();
                removed = true;
            }
        }

        if (removed)
        {
            _logger.LogInformation(
                "Litestream regen coordinator: tenant {TenantId} removed from exclusion set (reactivated).",
                tenantId);
            _channel.Writer.TryWrite(new RegenSignal(RegenTrigger.TenantReincluded, tenantId, "reactivated"));
        }
        else
        {
            _logger.LogDebug(
                "Litestream regen coordinator: tenant {TenantId} was not excluded; remove no-op.",
                tenantId);
        }

        return removed;
    }

    /// <summary>
    /// Replaces the exclusion set with the given collection in one atomic swap. Intended
    /// for startup seeding from the tenant store. Does NOT signal a regen — the hosted
    /// service's startup-initial-sync handles that separately so seeding + initial regen
    /// collapse into one YAML write.
    /// </summary>
    /// <param name="tenantIds">The tenant IDs to seed as the new exclusion set.</param>
    public void SeedExclusions(IEnumerable<Guid> tenantIds)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        var snapshot = tenantIds.ToFrozenSet();
        lock (_mutationGate)
        {
            _excludedTenantIds = snapshot;
        }
        _logger.LogInformation(
            "Litestream regen coordinator: seeded exclusion set with {Count} tenant(s) at startup.",
            snapshot.Count);
    }
}

/// <summary>The trigger source for a Litestream config regeneration.</summary>
public enum RegenTrigger
{
    /// <summary>Initial regen at hosted-service startup so the YAML reflects on-disk state.</summary>
    StartupInitialSync = 0,

    /// <summary>A <c>.db</c> file was created in the database directory (new tenant onboarded).</summary>
    FileCreated = 1,

    /// <summary>A <c>.db</c> file was deleted from the database directory (tenant fully removed).</summary>
    FileDeleted = 2,

    /// <summary>A tenant was added to the exclusion set via a lifecycle status change (Suspended/Inactive/Deleted).</summary>
    TenantExcluded = 3,

    /// <summary>A tenant was removed from the exclusion set via a lifecycle status change (reactivation to Active).</summary>
    TenantReincluded = 4,
}

/// <summary>
/// Payload describing why a regen was enqueued. The hosted service collapses bursts during
/// the debounce window; the latest signal's payload wins for log diagnostics.
/// </summary>
/// <param name="Trigger">The trigger source.</param>
/// <param name="TenantId">
/// The tenant ID for <see cref="RegenTrigger.TenantExcluded"/> and
/// <see cref="RegenTrigger.TenantReincluded"/>; <see langword="null"/> for file events and
/// startup sync.
/// </param>
/// <param name="Detail">
/// Free-form context: the file name for file events, or the lifecycle-transition reason for
/// tenant events; <see langword="null"/> for startup sync.
/// </param>
public readonly record struct RegenSignal(RegenTrigger Trigger, Guid? TenantId, string? Detail);
