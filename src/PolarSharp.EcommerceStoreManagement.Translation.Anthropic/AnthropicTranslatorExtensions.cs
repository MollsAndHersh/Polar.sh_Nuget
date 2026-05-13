using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.Anthropic;

/// <summary>DI registration for the Anthropic Claude translator.</summary>
public static class AnthropicTranslatorExtensions
{
    /// <summary>
    /// Registers the Anthropic translator as the Tier-2 (master) provider. Per-tenant
    /// (Tier 1) credentials on <c>TenantBusinessProfile</c> still take precedence via the
    /// resolver.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configure">Optional inline option configuration. The same values are
    /// merged into the master <see cref="EcommerceTranslationMasterOptions"/> if it's been
    /// bound from configuration.</param>
    public static IServiceCollection UseAnthropicTranslator(
        this IServiceCollection services,
        Action<EcommerceTranslationMasterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient("PolarSharp.Translation.Anthropic");
        services.AddSingleton<IPolarCatalogTranslatorFactory, AnthropicTranslatorFactory>();

        if (configure is not null)
        {
            services.Configure<EcommerceTranslationMasterOptions>(opts =>
            {
                opts.Provider = TranslationProvider.Anthropic;
                configure(opts);
                if (string.IsNullOrWhiteSpace(opts.Model)) opts.Model = AnthropicTranslatorDefaults.Model;
            });
        }
        return services;
    }
}
