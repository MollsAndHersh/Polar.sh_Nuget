using PolarSharp.MultiTenant.Identity.KeyCloak;

namespace PolarSharp.MultiTenant.Identity.KeyCloak.Tests;

/// <summary>
/// Verifies <see cref="KeyCloakOptions.ResolveClientSecret"/> precedence — env var beats
/// inline value beats null.
/// </summary>
public sealed class KeyCloakOptionsTests
{
    [Fact]
    public void Env_var_value_takes_precedence_over_inline_secret()
    {
        var envVarName = $"POLARSHARP_TEST_SECRET_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(envVarName, "secret-from-env");
        try
        {
            var opts = new KeyCloakOptions
            {
                ClientSecret = "inline-secret",
                ClientSecretEnvVar = envVarName,
            };
            Assert.Equal("secret-from-env", opts.ResolveClientSecret());
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void Falls_back_to_inline_secret_when_env_var_is_unset()
    {
        var opts = new KeyCloakOptions
        {
            ClientSecret = "inline-secret",
            ClientSecretEnvVar = "DEFINITELY_UNSET_ENV_VAR_" + Guid.NewGuid().ToString("N"),
        };
        Assert.Equal("inline-secret", opts.ResolveClientSecret());
    }

    [Fact]
    public void Returns_null_when_neither_inline_nor_env_var_is_set()
    {
        var opts = new KeyCloakOptions();
        Assert.Null(opts.ResolveClientSecret());
    }

    [Fact]
    public void Default_section_name_matches_the_documented_appsettings_path()
    {
        Assert.Equal("PolarSharp:KeyCloak", KeyCloakOptions.SectionName);
    }

    [Fact]
    public void Default_scopes_include_openid_profile_email()
    {
        var opts = new KeyCloakOptions();
        Assert.Contains("openid", opts.Scopes);
        Assert.Contains("profile", opts.Scopes);
        Assert.Contains("email", opts.Scopes);
    }

    [Fact]
    public void Default_tenant_id_claim_name_is_tenant_id()
    {
        Assert.Equal("tenant_id", new KeyCloakOptions().TenantIdClaim);
    }
}
