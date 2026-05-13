using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.Gemini;

/// <summary>DI registration for the Google Gemini translator.</summary>
public static class GeminiTranslatorExtensions
{
    /// <summary>Registers the Gemini translator as the Tier-2 (master) provider.</summary>
    public static IServiceCollection UseGeminiTranslator(
        this IServiceCollection services,
        Action<EcommerceTranslationMasterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient("PolarSharp.Translation.Gemini");
        services.AddSingleton<IPolarCatalogTranslatorFactory, GeminiTranslatorFactory>();

        if (configure is not null)
        {
            services.Configure<EcommerceTranslationMasterOptions>(opts =>
            {
                opts.Provider = TranslationProvider.Gemini;
                configure(opts);
                if (string.IsNullOrWhiteSpace(opts.Model)) opts.Model = GeminiTranslatorDefaults.Model;
            });
        }
        return services;
    }
}
