namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Read-only snapshot of the authenticated user's identity, current tenant context, and
/// effective permissions for the current request.
/// </summary>
/// <remarks>
/// <para>
/// Resolved per-request from <see cref="System.Security.Claims.ClaimsPrincipal"/> and the
/// active Finbuckle tenant context. For background services (no HTTP context),
/// <see cref="IsAuthenticated"/> returns <see langword="false"/> and all collections are empty;
/// background work that needs to perform tenant-scoped reads should hydrate the scope via
/// <c>IPolarTenantScopeInitializer</c>, then resolve a fresh <see cref="ICurrentUser"/>.
/// </para>
/// <para>
/// <strong>Permission semantics:</strong> when the user is an
/// <see cref="IsAppMasterAdmin"/> on a route annotated with <c>[AllowCrossTenant]</c>, the
/// <see cref="CurrentPermissions"/> set is computed as the union of the user's tenant-scoped
/// permissions PLUS the site-level permissions granted by the AppMasterAdmin role. On routes
/// without <c>[AllowCrossTenant]</c>, even AppMasterAdmins are bound to the standard
/// tenant-scoped permission lookup for their <see cref="CurrentTenantId"/>.
/// </para>
/// </remarks>
public interface ICurrentUser
{
    /// <summary>The Guid identifier of the authenticated user, or <see langword="null"/> when unauthenticated.</summary>
    Guid? UserId { get; }

    /// <summary>The user's email address, or <see langword="null"/> when unauthenticated.</summary>
    string? Email { get; }

    /// <summary>The user's user name, or <see langword="null"/> when unauthenticated.</summary>
    string? UserName { get; }

    /// <summary>The Guid identifier of the tenant the user is currently operating in, or <see langword="null"/> when no tenant is in scope.</summary>
    Guid? CurrentTenantId { get; }

    /// <summary>True when the user is an AppMasterAdmin (BOTH the DB flag AND the role claim are present).</summary>
    bool IsAppMasterAdmin { get; }

    /// <summary>True when an authenticated identity exists for the request.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The role names the user holds in <see cref="CurrentTenantId"/>. For AppMasterAdmins on cross-tenant routes, includes a synthetic <see cref="PolarRoles.TenantAdmin"/> grant.</summary>
    IReadOnlyList<string> CurrentRoles { get; }

    /// <summary>The effective <see cref="PolarPermission"/> set for the user in <see cref="CurrentTenantId"/>.</summary>
    IReadOnlyList<PolarPermission> CurrentPermissions { get; }

    /// <summary>Returns true when the user holds the named role within the current tenant context.</summary>
    /// <param name="role">The role name (typically a <see cref="PolarRoles"/> constant).</param>
    bool IsInRoleForCurrentTenant(string role);

    /// <summary>Returns true when the user holds the named permission within the current tenant context.</summary>
    /// <param name="permission">The permission to check.</param>
    bool HasPermissionInCurrentTenant(PolarPermission permission);
}
