using Microsoft.AspNetCore.Authorization;

namespace PolarSharp.MultiTenant.Identity.Authorization;

/// <summary>
/// Handler for <see cref="AppMasterAdminRequirement"/> — passes when
/// <see cref="ICurrentUser.IsAppMasterAdmin"/> is <see langword="true"/>.
/// </summary>
internal sealed class AppMasterAdminAuthorizationHandler : AuthorizationHandler<AppMasterAdminRequirement>
{
    private readonly ICurrentUser _currentUser;

    public AppMasterAdminAuthorizationHandler(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AppMasterAdminRequirement requirement)
    {
        if (_currentUser.IsAppMasterAdmin) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handler for <see cref="PolarPermissionRequirement"/> — passes when the user holds the
/// permission within their current tenant context.
/// </summary>
/// <remarks>
/// AppMasterAdmin bypass: site-level permissions (<see cref="RolePermissionMap.IsSiteLevel"/>)
/// pass when <see cref="ICurrentUser.IsAppMasterAdmin"/> is <see langword="true"/> regardless
/// of the cross-tenant signal. Tenant-scoped permissions for AppMasterAdmins still require
/// the <c>[AllowCrossTenant]</c> opt-in to take effect outside the user's CurrentTenantId —
/// see <see cref="ICurrentUser.CurrentPermissions"/> for the resolution semantics.
/// </remarks>
internal sealed class PolarPermissionAuthorizationHandler : AuthorizationHandler<PolarPermissionRequirement>
{
    private readonly ICurrentUser _currentUser;

    public PolarPermissionAuthorizationHandler(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PolarPermissionRequirement requirement)
    {
        // Site-level permissions: AppMasterAdmin always passes.
        if (RolePermissionMap.IsSiteLevel(requirement.Permission) && _currentUser.IsAppMasterAdmin)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Tenant-scoped permissions: standard CurrentPermissions check (which already factors
        // in the AppMasterAdmin + AllowCrossTenant signal).
        if (_currentUser.HasPermissionInCurrentTenant(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
