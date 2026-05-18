namespace PolarSharp.MultiTenant.Lifecycle;

/// <summary>Configuration knobs for <see cref="ITenantStatusService"/>.</summary>
/// <remarks>
/// Bound from the <c>PolarSharp:MultiTenant:TenantStatus</c> configuration section
/// (<see cref="SectionName"/>) by <c>AddPolarTenantLifecycle</c>.
/// </remarks>
public sealed class TenantStatusServiceOptions
{
    /// <summary>The configuration section name bound by <c>AddPolarTenantLifecycle</c>.</summary>
    public const string SectionName = "PolarSharp:MultiTenant:TenantStatus";

    /// <summary>
    /// Gets or sets a value indicating whether the service refuses to suspend a tenant
    /// whose <c>SiteManagerEmail</c> has not been verified.
    /// </summary>
    /// <value>
    /// When <c>true</c> (default), suspension is refused because the suspension
    /// notification would be unverifiable. Set <c>false</c> to allow suspension regardless
    /// of verification state. See also <see cref="SuspendUnverifiedTenantsAnyway"/> for an
    /// explicit override that preserves the audit trail.
    /// </value>
    public bool RequireVerifiedEmailForSuspension { get; set; } = true;

    /// <summary>
    /// Gets or sets a value that, when <c>true</c>, overrides
    /// <see cref="RequireVerifiedEmailForSuspension"/> and suspends unverified tenants anyway.
    /// </summary>
    /// <value>
    /// Default <c>false</c>. Provided as an explicit escape hatch rather than just flipping
    /// the other flag, so the audit trail captures the deliberate decision to bypass the
    /// verification guard.
    /// </value>
    public bool SuspendUnverifiedTenantsAnyway { get; set; } = false;

    /// <summary>
    /// Gets or sets the retention period (in days) for soft-deleted tenants before permanent removal.
    /// </summary>
    /// <value>
    /// Default 90. After this period, a separate cleanup process (out of scope for
    /// <see cref="ITenantStatusService"/>) permanently removes the tenant's data.
    /// </value>
    public int DeletedTenantRetentionDays { get; set; } = 90;
}
