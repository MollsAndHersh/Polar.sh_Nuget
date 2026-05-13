namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Maps a role name to the set of <see cref="PolarPermission"/>s it grants.
/// </summary>
/// <remarks>
/// Default implementation uses <see cref="RolePermissionMap.Defaults"/>; hosts can override
/// the registration to extend the mapping for custom roles, or substitute a database-backed
/// implementation that reads role-permission grants from a table.
/// </remarks>
public interface IRolePermissionResolver
{
    /// <summary>Returns the permission set granted by the named role. Empty when the role is unknown.</summary>
    /// <param name="roleName">A role name — typically a <see cref="PolarRoles"/> constant.</param>
    IReadOnlyCollection<PolarPermission> PermissionsForRole(string roleName);
}

/// <summary>
/// Default <see cref="IRolePermissionResolver"/> backed by <see cref="RolePermissionMap.Defaults"/>
/// merged with optional host-supplied overrides.
/// </summary>
internal sealed class DefaultRolePermissionResolver : IRolePermissionResolver
{
    private readonly IReadOnlyDictionary<string, IReadOnlySet<PolarPermission>> _map;

    public DefaultRolePermissionResolver(IDictionary<string, IReadOnlySet<PolarPermission>>? overrides = null)
    {
        if (overrides is null || overrides.Count == 0)
        {
            _map = RolePermissionMap.Defaults;
            return;
        }

        var merged = new Dictionary<string, IReadOnlySet<PolarPermission>>(RolePermissionMap.Defaults, StringComparer.Ordinal);
        foreach (var (role, perms) in overrides) merged[role] = perms;
        _map = merged;
    }

    public IReadOnlyCollection<PolarPermission> PermissionsForRole(string roleName) =>
        _map.TryGetValue(roleName, out var perms) ? (IReadOnlyCollection<PolarPermission>)perms : [];
}
