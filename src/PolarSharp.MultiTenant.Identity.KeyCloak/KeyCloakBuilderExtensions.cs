using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.MultiTenant.Identity.Extensions;

namespace PolarSharp.MultiTenant.Identity.KeyCloak;

/// <summary>DI registration for the KeyCloak SSO add-on.</summary>
public static class KeyCloakBuilderExtensions
{
    /// <summary>
    /// Wires KeyCloak SSO on top of an existing PolarSharp Identity registration. Adds the
    /// OIDC authentication handler pointed at the configured realm, registers the claims
    /// transformer, and binds <see cref="KeyCloakOptions"/> from configuration.
    /// </summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="configuration">App configuration — bound to <see cref="KeyCloakOptions"/> at <c>PolarSharp:KeyCloak</c>.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarIdentity(builder.Configuration)
    ///     .UseSqlServer(builder.Configuration)
    ///     .AddCoreIdentityServices()
    ///     .AddPolarKeyCloak(builder.Configuration);
    /// </code>
    /// </example>
    public static PolarIdentityBuilder AddPolarKeyCloak(this PolarIdentityBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.Services
            .AddOptions<KeyCloakOptions>()
            .Bind(configuration.GetSection(KeyCloakOptions.SectionName))
            .ValidateOnStart();

        builder.Services.TryAddSingleton<IKeyCloakRoleMapper>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeyCloakOptions>>().Value;
            return new KeyCloakRoleMapper(opts);
        });

        builder.Services.AddSingleton<IClaimsTransformation, KeyCloakClaimsTransformer>();

        var bound = configuration.GetSection(KeyCloakOptions.SectionName).Get<KeyCloakOptions>() ?? new KeyCloakOptions();
        if (!bound.Enabled) return builder;

        builder.Services
            .AddAuthentication(opts =>
            {
                opts.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opts.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(oidc =>
            {
                oidc.Authority = bound.Authority;
                oidc.ClientId = bound.ClientId;
                oidc.ClientSecret = bound.ResolveClientSecret();
                oidc.ResponseType = "code";
                oidc.SaveTokens = true;
                oidc.GetClaimsFromUserInfoEndpoint = true;
                foreach (var scope in (bound.Scopes ?? "openid profile email").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    oidc.Scope.Add(scope);
                }
            });

        return builder;
    }
}
