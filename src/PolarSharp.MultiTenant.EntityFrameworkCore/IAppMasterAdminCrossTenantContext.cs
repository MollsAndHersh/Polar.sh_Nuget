namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Ambient context that signals when the current request has been explicitly authorized for
/// AppMasterAdmin cross-tenant access via the <c>[AllowCrossTenant]</c> attribute.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by <c>PolarSharp.MultiTenant.Identity</c>'s authorization pipeline. The default
/// implementation (when Identity is not installed) returns <see langword="false"/> for every
/// request — meaning no cross-tenant access is ever granted without the Identity package.
/// </para>
/// <para>
/// <strong>Dual-flag verification:</strong> the flag is set to <see langword="true"/> ONLY
/// when BOTH conditions hold:
/// </para>
/// <list type="number">
///   <item><description>The route is annotated with <c>[AllowCrossTenant]</c></description></item>
///   <item><description>The authenticated user has <c>PolarApplicationUser.IsAppMasterAdmin</c> = <see langword="true"/> AND the <c>PolarRoles.AppMasterAdmin</c> role claim</description></item>
/// </list>
/// <para>
/// Failing either check leaves the flag <see langword="false"/>, and the standard tenant-scoped
/// query filter applies even for AppMasterAdmin users on non-opted-in routes.
/// </para>
/// </remarks>
public interface IAppMasterAdminCrossTenantContext
{
    /// <summary>Gets a value indicating whether the current request may bypass tenant-scope query filters.</summary>
    bool IsAllowedCrossTenantAccess { get; }
}
