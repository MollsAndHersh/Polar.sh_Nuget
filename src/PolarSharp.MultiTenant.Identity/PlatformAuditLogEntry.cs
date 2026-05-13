namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Site-level audit record — written when an <see cref="PolarApplicationUser.IsAppMasterAdmin"/>
/// user performs an operation that crosses tenant boundaries.
/// </summary>
/// <remarks>
/// <para>
/// <strong>NOT tenant-owned.</strong> This is the SaaS-provider's central audit ledger,
/// visible only to AppMasterAdmins with <see cref="PolarPermission.ViewPlatformAuditLog"/>.
/// It complements the per-tenant <c>AdminAuditLogEntry</c> (defined in
/// <c>PolarSharp.EcommerceStoreManagement</c>): when an AppMasterAdmin acts cross-tenant,
/// BOTH ledgers receive the entry. Cross-tenant writes are atomic — if the tenant audit
/// insert fails, the platform audit insert is also rolled back.
/// </para>
/// <para>
/// Retention is configurable via <c>PolarSharp:Identity:CrossTenantAccess:AuditPlatformLogRetentionDays</c>
/// (default: 2555 days / ~7 years for SaaS-provider compliance).
/// </para>
/// </remarks>
public class PlatformAuditLogEntry
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the AppMasterAdmin user who performed the operation.</summary>
    public Guid ActorUserId { get; set; }

    /// <summary>Snapshotted email address of the actor at the time of the operation. Preserved even if the user is later deleted.</summary>
    public string ActorEmail { get; set; } = "";

    /// <summary>The tenant whose data was operated on. (For non-cross-tenant ops by AppMasterAdmin, this equals the actor's <c>CurrentTenantId</c>.)</summary>
    public Guid TargetTenantId { get; set; }

    /// <summary>The CLR type name of the entity that was created / updated / deleted.</summary>
    public string EntityType { get; set; } = "";

    /// <summary>The Guid identifier of the affected entity instance.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The kind of mutation performed.</summary>
    public PlatformAuditAction Action { get; set; }

    /// <summary>UTC timestamp when the operation occurred.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Optional JSON snapshot of the entity's values BEFORE the mutation. Useful for rollback / forensic review.</summary>
    public string? BeforeValuesJson { get; set; }

    /// <summary>Optional JSON snapshot of the entity's values AFTER the mutation.</summary>
    public string? AfterValuesJson { get; set; }

    /// <summary><see langword="true"/> when the actor's <c>CurrentTenantId</c> differs from <see cref="TargetTenantId"/> — i.e., the actor was operating outside their normal tenant scope.</summary>
    public bool CrossTenantAccess { get; set; }

    /// <summary>Optional structured reason code for the cross-tenant operation. Captured from the request when the route invites it.</summary>
    public PlatformAuditJustificationKind? JustificationKind { get; set; }

    /// <summary>Optional free-form text explaining the cross-tenant operation. Required when <c>RequireJustificationText</c> = <see langword="true"/>.</summary>
    public string? JustificationText { get; set; }
}

/// <summary>The kind of mutation captured by a platform audit log entry.</summary>
public enum PlatformAuditAction
{
    /// <summary>A new entity was created.</summary>
    Create,
    /// <summary>An existing entity was updated.</summary>
    Update,
    /// <summary>An entity was deleted (or soft-deleted).</summary>
    Delete,
    /// <summary>A read operation that crossed tenant boundaries — auditable for compliance.</summary>
    Read,
}

/// <summary>Structured justification reason for cross-tenant operations. Searchable in the platform audit log.</summary>
public enum PlatformAuditJustificationKind
{
    /// <summary>Responding to a customer support ticket from the affected tenant.</summary>
    SupportTicket,
    /// <summary>Compliance / legal request requiring data review.</summary>
    Compliance,
    /// <summary>Active production incident — emergency access for diagnosis or remediation.</summary>
    Incident,
    /// <summary>Onboarding-related operation (e.g., assisting a new tenant's initial setup).</summary>
    Onboarding,
    /// <summary>Anything not covered by the structured reasons — free-form <c>JustificationText</c> should explain.</summary>
    Other,
}
