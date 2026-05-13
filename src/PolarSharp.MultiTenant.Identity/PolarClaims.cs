namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Claim type names PolarSharp emits and consumes during authorization.
/// </summary>
/// <remarks>
/// <para>
/// All claim types use the <c>polarsharp:</c> prefix to avoid collision with framework or
/// host-defined claims. The values are intentionally compact — claims travel in every
/// authenticated request as JWT/cookie payload.
/// </para>
/// </remarks>
public static class PolarClaims
{
    /// <summary>The Guid identifier of the authenticated user (string-encoded).</summary>
    public const string UserId = "polarsharp:user_id";

    /// <summary>The Guid identifier of the tenant the user is currently operating in (string-encoded).</summary>
    /// <remarks>
    /// For users with multiple memberships (M:N), this claim is set per-request — either by
    /// Finbuckle middleware (header/route/hostname/claim strategy) or by an explicit
    /// "switch tenant" action. AppMasterAdmins on cross-tenant routes may have this claim
    /// absent; in that case the user is operating site-globally.
    /// </remarks>
    public const string CurrentTenantId = "polarsharp:current_tenant_id";

    /// <summary>JSON array of <c>{tenantId, role}</c> pairs the user has active memberships in.</summary>
    public const string AvailableTenants = "polarsharp:tenants";

    /// <summary>A single <see cref="PolarPermission"/> grant within the current tenant context. May appear multiple times.</summary>
    public const string Permission = "polarsharp:permission";

    /// <summary>Boolean flag (<c>true</c>/<c>false</c>) — site-level bypass marker. Set ONLY when the user's DB-stored <c>IsAppMasterAdmin</c> is true AND they hold the <see cref="PolarRoles.AppMasterAdmin"/> role.</summary>
    public const string IsAppMasterAdmin = "polarsharp:is_app_master_admin";
}
