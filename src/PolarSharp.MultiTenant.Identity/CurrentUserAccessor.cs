using System.Security.Claims;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Default <see cref="ICurrentUser"/> implementation backed by <see cref="IHttpContextAccessor"/>
/// and Finbuckle's <see cref="IMultiTenantContextAccessor"/>.
/// </summary>
internal sealed class CurrentUserAccessor : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContext;
    private readonly IMultiTenantContextAccessor _tenantContext;
    private readonly IAppMasterAdminCrossTenantSignal _crossTenantSignal;
    private readonly IRolePermissionResolver _permissionResolver;

    public CurrentUserAccessor(
        IHttpContextAccessor httpContext,
        IMultiTenantContextAccessor tenantContext,
        IAppMasterAdminCrossTenantSignal crossTenantSignal,
        IRolePermissionResolver permissionResolver)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(crossTenantSignal);
        ArgumentNullException.ThrowIfNull(permissionResolver);
        _httpContext = httpContext;
        _tenantContext = tenantContext;
        _crossTenantSignal = crossTenantSignal;
        _permissionResolver = permissionResolver;
    }

    private ClaimsPrincipal? Principal => _httpContext.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(PolarClaims.UserId)
            ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public string? UserName => Principal?.Identity?.Name;

    public Guid? CurrentTenantId
    {
        get
        {
            // Prefer the explicit claim (e.g. set by an "I want to operate in tenant X" switch)
            // over the Finbuckle-resolved context — the claim represents an intentional choice
            // by an M:N user.
            var claim = Principal?.FindFirstValue(PolarClaims.CurrentTenantId);
            if (Guid.TryParse(claim, out var fromClaim)) return fromClaim;

            var fromFinbuckle = _tenantContext.MultiTenantContext?.TenantInfo?.Id;
            return Guid.TryParse(fromFinbuckle, out var fb) ? fb : null;
        }
    }

    public bool IsAppMasterAdmin
    {
        get
        {
            // Dual-flag verification: both the claim AND the role grant must be present. A
            // tampered token that adds the role without the boolean claim — or vice versa —
            // is rejected.
            var hasClaim = string.Equals(
                Principal?.FindFirstValue(PolarClaims.IsAppMasterAdmin),
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase);
            var hasRole = Principal?.IsInRole(PolarRoles.AppMasterAdmin) == true;
            return hasClaim && hasRole;
        }
    }

    public IReadOnlyList<string> CurrentRoles
    {
        get
        {
            if (Principal is null) return [];
            return [.. Principal.FindAll(ClaimTypes.Role).Select(c => c.Value)];
        }
    }

    public IReadOnlyList<PolarPermission> CurrentPermissions
    {
        get
        {
            if (Principal is null) return [];

            // Direct permission claims (host can also stamp these explicitly via KeyCloak / OIDC
            // claim mappers).
            var direct = Principal.FindAll(PolarClaims.Permission)
                .Select(c => Enum.TryParse<PolarPermission>(c.Value, ignoreCase: true, out var p) ? p : (PolarPermission?)null)
                .Where(p => p.HasValue)
                .Select(p => p!.Value);

            // Role-derived permissions — resolved against the role-permission map.
            var roleDerived = CurrentRoles.SelectMany(_permissionResolver.PermissionsForRole);

            // AppMasterAdmin on a [AllowCrossTenant] route picks up site-level perms.
            var siteLevel = (IsAppMasterAdmin && _crossTenantSignal.IsAllowedCrossTenantAccess)
                ? _permissionResolver.PermissionsForRole(PolarRoles.AppMasterAdmin)
                : [];

            return [.. direct.Concat(roleDerived).Concat(siteLevel).Distinct()];
        }
    }

    public bool IsInRoleForCurrentTenant(string role) => CurrentRoles.Contains(role, StringComparer.Ordinal);

    public bool HasPermissionInCurrentTenant(PolarPermission permission) => CurrentPermissions.Contains(permission);
}
