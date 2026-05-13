using System.Security.Claims;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.KeyCloak;

namespace PolarSharp.MultiTenant.Identity.KeyCloak.Tests;

/// <summary>
/// Locks the claim-rewriting contract — KeyCloak realm roles → PolarSharp roles, AppMasterAdmin
/// dual-flag emission, tenant id propagation. The transformer is the linchpin of the SSO
/// integration so its behaviour is exhaustively tested.
/// </summary>
public sealed class KeyCloakClaimsTransformerTests
{
    [Fact]
    public async Task Realm_role_in_JSON_realm_access_claim_is_mapped_to_polar_role()
    {
        var transformer = NewTransformer(
            mappings: new() { ["realm-admin"] = PolarRoles.TenantAdmin });
        var principal = BuildPrincipal(realmAccessJson: """{"roles":["realm-admin","other-role"]}""");

        var result = await transformer.TransformAsync(principal);

        Assert.Contains(result.FindAll(ClaimTypes.Role), c => c.Value == PolarRoles.TenantAdmin);
        // Unmapped role should NOT appear under ClaimTypes.Role.
        Assert.DoesNotContain(result.FindAll(ClaimTypes.Role), c => c.Value == "other-role");
    }

    [Fact]
    public async Task Realm_role_emitted_as_separate_roles_claim_is_also_mapped()
    {
        // Some KeyCloak configurations push each role as a separate "roles" claim instead of
        // a JSON-wrapped realm_access object. The transformer handles both shapes.
        var transformer = NewTransformer(
            mappings: new() { ["realm-merchant"] = PolarRoles.TenantUser });
        var identity = new ClaimsIdentity("oidc");
        identity.AddClaim(new Claim("roles", "realm-merchant"));
        identity.AddClaim(new Claim("roles", "noise"));
        var principal = new ClaimsPrincipal(identity);

        var result = await transformer.TransformAsync(principal);
        Assert.Contains(result.FindAll(ClaimTypes.Role), c => c.Value == PolarRoles.TenantUser);
    }

    [Fact]
    public async Task AppMasterAdmin_role_triggers_dual_flag_emission()
    {
        var transformer = NewTransformer(
            mappings: new() { ["realm-app-master"] = PolarRoles.AppMasterAdmin });
        var principal = BuildPrincipal(realmAccessJson: """{"roles":["realm-app-master"]}""");

        var result = await transformer.TransformAsync(principal);
        Assert.Contains(result.FindAll(ClaimTypes.Role), c => c.Value == PolarRoles.AppMasterAdmin);
        Assert.Equal(bool.TrueString, result.FindFirst(PolarClaims.IsAppMasterAdmin)?.Value);
    }

    [Fact]
    public async Task Non_AppMasterAdmin_roles_do_NOT_set_the_dual_flag()
    {
        // Defense — only AppMasterAdmin gets the dual flag. Other mappings must not leak it.
        var transformer = NewTransformer(
            mappings: new() { ["realm-admin"] = PolarRoles.TenantAdmin });
        var principal = BuildPrincipal(realmAccessJson: """{"roles":["realm-admin"]}""");

        var result = await transformer.TransformAsync(principal);
        Assert.Null(result.FindFirst(PolarClaims.IsAppMasterAdmin));
    }

    [Fact]
    public async Task Tenant_id_claim_is_propagated_to_polarsharp_current_tenant_id_claim()
    {
        var transformer = NewTransformer(tenantIdClaim: "tenant_id");
        var identity = new ClaimsIdentity("oidc");
        identity.AddClaim(new Claim("tenant_id", "tenant-acme"));
        var principal = new ClaimsPrincipal(identity);

        var result = await transformer.TransformAsync(principal);
        Assert.Equal("tenant-acme", result.FindFirst(PolarClaims.CurrentTenantId)?.Value);
    }

    [Fact]
    public async Task Existing_polarsharp_tenant_id_claim_is_NOT_overwritten()
    {
        // The host may have stamped a tenant via cookie / session / claim chaining — the
        // transformer must not clobber an explicit choice.
        var transformer = NewTransformer(tenantIdClaim: "tenant_id");
        var identity = new ClaimsIdentity("oidc");
        identity.AddClaim(new Claim("tenant_id", "tenant-keycloak"));
        identity.AddClaim(new Claim(PolarClaims.CurrentTenantId, "tenant-already-set"));
        var principal = new ClaimsPrincipal(identity);

        var result = await transformer.TransformAsync(principal);
        Assert.Equal("tenant-already-set", result.FindFirst(PolarClaims.CurrentTenantId)?.Value);
    }

    [Fact]
    public async Task Transform_is_idempotent_repeated_calls_do_not_duplicate_role_claims()
    {
        var transformer = NewTransformer(
            mappings: new() { ["realm-admin"] = PolarRoles.TenantAdmin });
        var principal = BuildPrincipal(realmAccessJson: """{"roles":["realm-admin"]}""");

        await transformer.TransformAsync(principal);
        await transformer.TransformAsync(principal);
        await transformer.TransformAsync(principal);

        var tenantAdminRoleCount = principal.FindAll(ClaimTypes.Role).Count(c => c.Value == PolarRoles.TenantAdmin);
        Assert.Equal(1, tenantAdminRoleCount);
    }

    [Fact]
    public async Task Unauthenticated_principal_passes_through_unchanged()
    {
        var transformer = NewTransformer(
            mappings: new() { ["realm-admin"] = PolarRoles.TenantAdmin });
        // Unauthenticated identity — no AuthenticationType.
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await transformer.TransformAsync(principal);
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task Disabled_transformer_passes_through_unchanged()
    {
        var transformer = NewTransformer(
            mappings: new() { ["realm-admin"] = PolarRoles.TenantAdmin },
            enabled: false);
        var principal = BuildPrincipal(realmAccessJson: """{"roles":["realm-admin"]}""");

        var result = await transformer.TransformAsync(principal);
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task Malformed_realm_access_JSON_yields_no_role_claims_without_throwing()
    {
        var transformer = NewTransformer(
            mappings: new() { ["realm-admin"] = PolarRoles.TenantAdmin });
        var identity = new ClaimsIdentity("oidc");
        identity.AddClaim(new Claim("realm_access", "this isn't JSON"));
        var principal = new ClaimsPrincipal(identity);

        var result = await transformer.TransformAsync(principal);
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task Realm_access_with_missing_roles_array_yields_no_role_claims()
    {
        var transformer = NewTransformer(
            mappings: new() { ["realm-admin"] = PolarRoles.TenantAdmin });
        var identity = new ClaimsIdentity("oidc");
        identity.AddClaim(new Claim("realm_access", """{"groups":["unused"]}"""));
        var principal = new ClaimsPrincipal(identity);

        var result = await transformer.TransformAsync(principal);
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static KeyCloakClaimsTransformer NewTransformer(
        Dictionary<string, string>? mappings = null,
        string tenantIdClaim = "tenant_id",
        bool enabled = true)
    {
        var options = new KeyCloakOptions
        {
            Enabled = enabled,
            TenantIdClaim = tenantIdClaim,
            RoleMapping = mappings ?? [],
        };
        return new KeyCloakClaimsTransformer(new KeyCloakRoleMapper(options), Options.Create(options));
    }

    private static ClaimsPrincipal BuildPrincipal(string realmAccessJson)
    {
        var identity = new ClaimsIdentity("oidc");
        identity.AddClaim(new Claim("realm_access", realmAccessJson));
        return new ClaimsPrincipal(identity);
    }
}
