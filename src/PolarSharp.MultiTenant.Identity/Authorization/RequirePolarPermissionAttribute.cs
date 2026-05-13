using Microsoft.AspNetCore.Authorization;

namespace PolarSharp.MultiTenant.Identity.Authorization;

/// <summary>
/// Tenant-scoped permission gate — requires the authenticated user to hold the named
/// <see cref="PolarPermission"/> within their current tenant context.
/// </summary>
/// <remarks>
/// <para>
/// AppMasterAdmin bypass requires a co-attribute: <see cref="AllowCrossTenantAttribute"/>.
/// Without that opt-in, even an AppMasterAdmin's permission set is computed against their
/// <see cref="ICurrentUser.CurrentTenantId"/> — preventing accidental cross-tenant data
/// access on routes designed for tenant scope.
/// </para>
/// <para>
/// Multiple attributes compose with AND semantics — annotating a method with
/// <c>[RequirePolarPermission(EditCatalog)]</c> AND <c>[RequirePolarPermission(PublishCatalog)]</c>
/// requires both permissions.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePolarPermissionAttribute : AuthorizeAttribute
{
    /// <summary>The permission required for this route.</summary>
    public PolarPermission Permission { get; }

    /// <summary>Initializes the attribute requiring the named permission.</summary>
    /// <param name="permission">The required permission.</param>
    public RequirePolarPermissionAttribute(PolarPermission permission)
    {
        Permission = permission;
        Policy = $"{PolarAuthorizationPolicies.PermissionPrefix}{permission}";
    }
}
