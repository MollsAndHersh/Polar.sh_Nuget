using Microsoft.AspNetCore.Authorization;

namespace PolarSharp.MultiTenant.Identity.Authorization;

/// <summary>
/// SITE-LEVEL gate — requires the user to be an AppMasterAdmin (DB flag + role claim).
/// </summary>
/// <remarks>
/// Use on routes that have NO meaningful tenant scope (e.g., "list all tenants",
/// "platform-wide audit log", "manage other AppMasterAdmins"). For routes that operate on a
/// specific tenant but allow AppMasterAdmin bypass of tenant scope, combine
/// <see cref="RequirePolarPermissionAttribute"/> with <see cref="AllowCrossTenantAttribute"/>
/// instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAppMasterAdminAttribute : AuthorizeAttribute
{
    /// <summary>Initializes the site-level attribute. Always uses the <see cref="PolarAuthorizationPolicies.AppMasterAdmin"/> policy.</summary>
    public RequireAppMasterAdminAttribute() => Policy = PolarAuthorizationPolicies.AppMasterAdmin;
}
