using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI;

/// <summary>DI registration for the Azure OpenAI translator.</summary>
public static class AzureOpenAiTranslatorExtensions
{
    /// <summary>Registers the Azure OpenAI translator as the Tier-2 (master) provider. <c>Endpoint</c> is required; <c>Model</c> is the deployment name.</summary>
    public static IServiceCollection UseAzureOpenAiTranslator(
        this IServiceCollection services,
        Action<EcommerceTranslationMasterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient("PolarSharp.Translation.AzureOpenAI");
        services.AddSingleton<IPolarCatalogTranslatorFactory, AzureOpenAiTranslatorFactory>();

        if (configure is not null)
        {
            services.Configure<EcommerceTranslationMasterOptions>(opts =>
            {
                opts.Provider = TranslationProvider.AzureOpenAI;
                configure(opts);
            });
        }
        return services;
    }
}
