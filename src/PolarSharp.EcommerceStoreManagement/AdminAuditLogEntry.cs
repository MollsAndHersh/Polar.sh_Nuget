using System.Text.Json.Nodes;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement;

/// <summary>
/// Records a single admin-initiated mutation against a tenant-scoped catalog entity. Written
/// by <c>AuditLogSaveChangesInterceptor</c> on every <c>SaveChanges</c>; the host can query
/// the trail via the standard catalog repository (or, when Reporting is installed, via the
/// reporting client's audit-trail endpoint).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Cross-tenant marker.</strong> When an AppMasterAdmin operates on a tenant
/// outside their own scope (via <c>[AllowCrossTenant]</c>), <see cref="CrossTenantAccess"/>
/// is true. In that case the audit-log interceptor ALSO writes a
/// <c>PlatformAuditLogEntry</c> row in <c>PolarSharp.MultiTenant.Identity</c> within the same
/// transaction — so the trail is visible to BOTH the affected tenant's operators (this row)
/// AND the SaaS provider's central audit ledger (platform row).
/// </para>
/// </remarks>
public sealed class AdminAuditLogEntry : ITenantOwned, IFakeDataAware
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The tenant the audited entity belongs to. EF Core's <see cref="ITenantOwned"/> filter requires this as a regular settable string property (explicit-interface impls aren't reflectable by EF expressions).</summary>
    public string TenantId { get; set; } = "";

    /// <summary>Convenience accessor — parses <see cref="TenantId"/> as a <see cref="Guid"/> for downstream Identity / Reporting integrations.</summary>
    public Guid TenantGuid => Guid.TryParse(TenantId, out var g) ? g : Guid.Empty;

    /// <summary>Guid of the user who performed the mutation. Resolved via <c>IAuditLogActorProvider</c>.</summary>
    public Guid ActorUserId { get; set; }

    /// <summary>Snapshotted email of the actor at the time of the operation.</summary>
    public string ActorEmail { get; set; } = "";

    /// <summary>CLR type name of the mutated entity (e.g. <c>"LocalProductEntity"</c>).</summary>
    public string EntityType { get; set; } = "";

    /// <summary>Identifier of the mutated entity instance.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Whether the mutation was a create / update / delete.</summary>
    public AuditAction Action { get; set; }

    /// <summary>UTC of the mutation.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>JSON snapshot of the entity's values BEFORE the mutation. Null for creates.</summary>
    public JsonNode? BeforeValues { get; set; }

    /// <summary>JSON snapshot of the entity's values AFTER the mutation. Null for deletes.</summary>
    public JsonNode? AfterValues { get; set; }

    /// <summary>Names of fields that changed. Empty for creates and deletes.</summary>
    public IReadOnlyList<string> ChangedFields { get; set; } = [];

    /// <inheritdoc/>
    public bool IsFakeData { get; set; }

    /// <summary>True when the actor's current tenant differs from <see cref="TenantGuid"/> — i.e. an AppMasterAdmin cross-tenant operation.</summary>
    public bool CrossTenantAccess { get; set; }

    /// <summary>Free-form justification text captured from <c>[AllowCrossTenant]</c> when <c>RequireJustificationText = true</c>.</summary>
    public string? CrossTenantJustification { get; set; }
}
