namespace PolarSharp.MultiTenant.Lifecycle;

/// <summary>Tenant lifecycle status values.</summary>
/// <remarks>
/// <para>
/// Distinct from <c>PolarSharp.BaseEntities.PolarOrganizationStatus</c> — the latter
/// is the merchant organization's status as reported by Polar.sh's own API. This enum tracks
/// the PolarSharp-internal lifecycle of a tenant inside the host application's registry
/// (suspended for billing dispute, soft-deleted pending retention expiry, etc.) and drives
/// PolarSharp-side enforcement (e.g., Litestream replication exclusion, lifecycle notifications).
/// </para>
/// </remarks>
public enum TenantStatus
{
    /// <summary>The tenant is fully active. Default state on creation.</summary>
    Active = 0,

    /// <summary>
    /// The tenant is suspended — temporarily disabled (typically by SaaS admin
    /// intervention or an automatic rule). Reversible via <see cref="ITenantStatusService.ReactivateAsync"/>.
    /// </summary>
    /// <remarks>
    /// Suspension is intended for short-to-medium-term enforcement (e.g., billing
    /// dispute, terms-of-service violation under review). Tenant data is retained
    /// fully; only API access is gated.
    /// </remarks>
    Suspended = 1,

    /// <summary>
    /// The tenant is inactive — tenant-initiated closure or long-term disabled
    /// status. Reversible but expected to be long-term.
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// The tenant is soft-deleted. Data is preserved for the configured retention
    /// period; reactivation requires AppMasterAdmin intervention. After retention
    /// expires, data is permanently removed.
    /// </summary>
    Deleted = 3,
}
