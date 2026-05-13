namespace PolarSharp.MultiTenant.Identity.KeyCloak;

/// <summary>
/// Configuration for the optional KeyCloak SSO integration. Bound from
/// <c>PolarSharp:KeyCloak</c> in <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// When this package is installed and <see cref="Enabled"/> is <see langword="true"/>:
/// </para>
/// <list type="bullet">
///   <item><description>KeyCloak is the source of truth for credentials and role membership.</description></item>
///   <item><description>PolarSharp's <c>PolarUserDbContext</c> remains the source of truth for application-specific user state (last login, onboarded-at, M:N tenant memberships).</description></item>
///   <item><description>A <c>KeyCloakClaimsTransformer</c> maps incoming KeyCloak realm-roles to PolarSharp roles via <see cref="RoleMapping"/>.</description></item>
///   <item><description>If a user holds a KeyCloak role mapped to <see cref="PolarSharp.MultiTenant.Identity.PolarRoles.AppMasterAdmin"/>, the transformer additionally stamps the <see cref="PolarSharp.MultiTenant.Identity.PolarClaims.IsAppMasterAdmin"/> claim — satisfying the dual-flag check used by <c>ICurrentUser.IsAppMasterAdmin</c>.</description></item>
/// </list>
/// </remarks>
public sealed class KeyCloakOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PolarSharp:KeyCloak";

    /// <summary>Master switch — when false the package's services are registered but inert.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The KeyCloak realm-base URL (e.g. <c>https://keycloak.example.com/realms/myrealm</c>).</summary>
    public string? Authority { get; set; }

    /// <summary>The OIDC client id registered with KeyCloak.</summary>
    public string? ClientId { get; set; }

    /// <summary>The OIDC client secret. Prefer <see cref="ClientSecretEnvVar"/> in production — secrets in <c>appsettings.json</c> are easy to leak.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Environment-variable name to read the client secret from. Takes precedence over <see cref="ClientSecret"/>.</summary>
    public string? ClientSecretEnvVar { get; set; }

    /// <summary>OIDC scopes to request. Default <c>"openid profile email"</c>.</summary>
    public string Scopes { get; set; } = "openid profile email";

    /// <summary>The OIDC claim KeyCloak emits to convey the user's current tenant id (set up via a KeyCloak attribute-protocol-mapper). Default <c>"tenant_id"</c>.</summary>
    public string TenantIdClaim { get; set; } = "tenant_id";

    /// <summary>
    /// Mapping from KeyCloak realm-role name → PolarSharp role name (typically a
    /// <c>PolarRoles.*</c> constant). On every authenticated request the claims transformer
    /// emits one <see cref="System.Security.Claims.ClaimTypes.Role"/> claim per matching mapping.
    /// </summary>
    /// <remarks>
    /// Roles in KeyCloak that have no entry here are passed through unmapped — useful for
    /// host-defined custom roles. To explicitly drop a KeyCloak role, simply omit it from
    /// the map.
    /// </remarks>
    public Dictionary<string, string> RoleMapping { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Resolves the effective client secret — env var takes precedence over the inline value.</summary>
    public string? ResolveClientSecret()
    {
        if (!string.IsNullOrEmpty(ClientSecretEnvVar))
        {
            var fromEnv = Environment.GetEnvironmentVariable(ClientSecretEnvVar);
            if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;
        }
        return ClientSecret;
    }
}
