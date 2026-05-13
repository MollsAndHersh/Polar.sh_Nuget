using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.Identity.KeyCloak;

/// <summary>
/// <see cref="IClaimsTransformation"/> that rewrites a KeyCloak-authenticated principal into
/// PolarSharp's role/claim contract.
/// </summary>
/// <remarks>
/// <para>
/// Runs on every authenticated request — must be idempotent (the same principal may pass
/// through more than once per request lifecycle). The transformer:
/// </para>
/// <list type="number">
///   <item><description>Extracts realm roles from KeyCloak's <c>realm_access.roles</c> claim (a JSON object the OIDC handler unpacks into a single claim with a JSON string value).</description></item>
///   <item><description>For each realm role, applies <see cref="IKeyCloakRoleMapper.Map"/> to find a PolarSharp role; emits a <see cref="ClaimTypes.Role"/> claim with the mapped name.</description></item>
///   <item><description>When any mapped role equals <see cref="PolarRoles.AppMasterAdmin"/>, additionally stamps <see cref="PolarClaims.IsAppMasterAdmin"/> = <c>"True"</c> — satisfying the dual-flag check in <c>ICurrentUser.IsAppMasterAdmin</c>.</description></item>
///   <item><description>Copies KeyCloak's <see cref="KeyCloakOptions.TenantIdClaim"/> value (when present) into <see cref="PolarClaims.CurrentTenantId"/> so PolarSharp's per-tenant authorization can resolve the current tenant from the token.</description></item>
/// </list>
/// </remarks>
public sealed class KeyCloakClaimsTransformer : IClaimsTransformation
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IKeyCloakRoleMapper _mapper;
    private readonly KeyCloakOptions _options;

    /// <summary>Initializes the transformer.</summary>
    public KeyCloakClaimsTransformer(IKeyCloakRoleMapper mapper, IOptions<KeyCloakOptions> options)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(options);
        _mapper = mapper;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!_options.Enabled) return Task.FromResult(principal);
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return Task.FromResult(principal);

        var existingRoles = identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);

        // ── Realm roles → PolarSharp roles ──
        foreach (var kcRole in ExtractRealmRoles(identity))
        {
            var polarRole = _mapper.Map(kcRole);
            if (polarRole is null) continue;
            if (existingRoles.Add(polarRole))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, polarRole));
            }
        }

        // ── AppMasterAdmin dual-flag ──
        if (existingRoles.Contains(PolarRoles.AppMasterAdmin)
            && identity.FindFirst(PolarClaims.IsAppMasterAdmin) is null)
        {
            identity.AddClaim(new Claim(PolarClaims.IsAppMasterAdmin, bool.TrueString));
        }

        // ── Tenant-id propagation ──
        if (identity.FindFirst(PolarClaims.CurrentTenantId) is null)
        {
            var tenantIdRaw = identity.FindFirst(_options.TenantIdClaim)?.Value;
            if (!string.IsNullOrWhiteSpace(tenantIdRaw))
            {
                identity.AddClaim(new Claim(PolarClaims.CurrentTenantId, tenantIdRaw));
            }
        }

        return Task.FromResult(principal);
    }

    /// <summary>
    /// Parses KeyCloak's <c>realm_access</c> claim payload (a JSON object) and returns the
    /// realm-role names. Tolerates the claim being absent or malformed — returns an empty
    /// sequence in either case.
    /// </summary>
    internal static IEnumerable<string> ExtractRealmRoles(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        // KeyCloak emits realm_access as a JSON value like {"roles":["admin","user"]}.
        // The OIDC handler may surface it as one of:
        //   - a single claim whose value is the JSON string, OR
        //   - one claim per role (when "Add to ID token" is configured for a Roles mapper)
        // We handle both. Snapshot claim values up front — the caller mutates the identity's
        // claim collection mid-iteration (adding mapped ClaimTypes.Role claims), so a lazy
        // enumeration over identity.FindAll(...) would throw "Collection was modified".
        var rolesFromRealmAccess = identity.FindFirst("realm_access") is { } realmAccess
            ? ParseJsonRoles(realmAccess.Value)
            : [];
        var directRoleClaims = identity.FindAll("roles").Select(c => c.Value).ToList();

        return rolesFromRealmAccess.Concat(directRoleClaims);
    }

    private static IEnumerable<string> ParseJsonRoles(string realmAccessJson)
    {
        if (string.IsNullOrWhiteSpace(realmAccessJson)) return [];
        try
        {
            using var doc = JsonDocument.Parse(realmAccessJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return [];
            if (!doc.RootElement.TryGetProperty("roles", out var rolesEl) || rolesEl.ValueKind != JsonValueKind.Array) return [];
            var result = new List<string>();
            foreach (var role in rolesEl.EnumerateArray())
            {
                if (role.ValueKind == JsonValueKind.String && role.GetString() is { Length: > 0 } s)
                {
                    result.Add(s);
                }
            }
            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
