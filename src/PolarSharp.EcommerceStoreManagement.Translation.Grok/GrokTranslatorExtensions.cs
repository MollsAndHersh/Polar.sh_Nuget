using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.Grok;

/// <summary>DI registration for the xAI Grok translator.</summary>
public static class GrokTranslatorExtensions
{
    /// <summary>Registers the Grok translator as the Tier-2 (master) provider.</summary>
    public static IServiceCollection UseGrokTranslator(
        this IServiceCollection services,
        Action<EcommerceTranslationMasterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient("PolarSharp.Translation.Grok");
        services.AddSingleton<IPolarCatalogTranslatorFactory, GrokTranslatorFactory>();

        if (configure is not null)
        {
            services.Configure<EcommerceTranslationMasterOptions>(opts =>
            {
                opts.Provider = TranslationProvider.Grok;
                configure(opts);
                if (string.IsNullOrWhiteSpace(opts.Model)) opts.Model = GrokTranslatorDefaults.Model;
            });
        }
        return services;
    }
}
