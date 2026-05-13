namespace PolarSharp.MultiTenant.Identity.KeyCloak;

/// <summary>
/// Pure mapper from a KeyCloak realm-role string to a PolarSharp role string. Wraps the
/// <see cref="KeyCloakOptions.RoleMapping"/> dictionary in a typed API so callers don't
/// indexing into raw <c>Dictionary&lt;string,string&gt;</c>.
/// </summary>
public interface IKeyCloakRoleMapper
{
    /// <summary>Returns the PolarSharp role name for the given KeyCloak realm-role, or <see langword="null"/> when the role is not mapped.</summary>
    string? Map(string keycloakRoleName);

    /// <summary>Convenience — returns every mapped PolarSharp role for the supplied KeyCloak roles.</summary>
    IEnumerable<string> MapMany(IEnumerable<string> keycloakRoleNames);
}

/// <summary>Default <see cref="IKeyCloakRoleMapper"/> backed by <see cref="KeyCloakOptions.RoleMapping"/>.</summary>
public sealed class KeyCloakRoleMapper : IKeyCloakRoleMapper
{
    private readonly IReadOnlyDictionary<string, string> _mapping;

    /// <summary>Initializes the mapper with the supplied options.</summary>
    public KeyCloakRoleMapper(KeyCloakOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _mapping = options.RoleMapping ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public string? Map(string keycloakRoleName)
    {
        ArgumentException.ThrowIfNullOrEmpty(keycloakRoleName);
        return _mapping.TryGetValue(keycloakRoleName, out var polarRole) ? polarRole : null;
    }

    /// <inheritdoc/>
    public IEnumerable<string> MapMany(IEnumerable<string> keycloakRoleNames)
    {
        ArgumentNullException.ThrowIfNull(keycloakRoleNames);
        foreach (var kcRole in keycloakRoleNames)
        {
            if (string.IsNullOrEmpty(kcRole)) continue;
            if (_mapping.TryGetValue(kcRole, out var polarRole)) yield return polarRole;
        }
    }
}
