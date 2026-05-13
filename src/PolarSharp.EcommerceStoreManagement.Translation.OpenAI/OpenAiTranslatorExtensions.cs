using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.OpenAI;

/// <summary>DI registration for the OpenAI translator.</summary>
public static class OpenAiTranslatorExtensions
{
    /// <summary>Registers the OpenAI translator as the Tier-2 (master) provider.</summary>
    public static IServiceCollection UseOpenAiTranslator(
        this IServiceCollection services,
        Action<EcommerceTranslationMasterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient("PolarSharp.Translation.OpenAI");
        services.AddSingleton<IPolarCatalogTranslatorFactory, OpenAiTranslatorFactory>();

        if (configure is not null)
        {
            services.Configure<EcommerceTranslationMasterOptions>(opts =>
            {
                opts.Provider = TranslationProvider.OpenAI;
                configure(opts);
                if (string.IsNullOrWhiteSpace(opts.Model)) opts.Model = OpenAiTranslatorDefaults.Model;
            });
        }
        return services;
    }
}
