using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Reading;
using PolarSharp.EcommerceStoreManagement.Reading;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Translation;

/// <summary>
/// DI registration for the EF-backed translation services (provider resolver + tenant
/// config lookup).
/// </summary>
/// <remarks>
/// <para>
/// This extension covers only the translation surface. The full <c>AddPolarEcommerce()</c>
/// orchestrator (planned for v1.3.G) will compose this with the publisher, refund service,
/// license validator, and other implementations once those land.
/// </para>
/// <para>
/// Hosts must also register at least one <see cref="IPolarCatalogTranslatorFactory"/>
/// (via the corresponding <c>.Translation.Anthropic</c> / <c>.OpenAI</c> /
/// <c>.AzureOpenAI</c> / <c>.Gemini</c> / <c>.Grok</c> package's
/// <c>UseXxxTranslator</c> extension) for either tier to produce a translator. With no
/// factories registered the resolver always returns <see langword="null"/> (Tier 3:
/// disabled).
/// </para>
/// </remarks>
public static class TranslationServicesExtensions
{
    /// <summary>
    /// Registers the EF-backed tenant translation config lookup and the 3-tier translation
    /// provider resolver. Binds <see cref="EcommerceTranslationMasterOptions"/> from
    /// <c>PolarSharp:EcommerceStoreManagement:Translation</c> on the supplied configuration.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">Application configuration containing the master-tier options.</param>
    public static IServiceCollection AddPolarCatalogTranslation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<EcommerceTranslationMasterOptions>(
            configuration.GetSection(EcommerceTranslationMasterOptions.SectionName));
        services.Configure<TranslationCacheOptions>(
            configuration.GetSection(TranslationCacheOptions.SectionName));

        services.AddDataProtection();
        services.AddMemoryCache();
        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);

        services.AddScoped<ITenantTranslationConfigLookup, EfTenantTranslationConfigLookup>();
        services.AddScoped<ITranslationProviderResolver, EfTranslationProviderResolver>();
        services.AddScoped<ITranslationRepository, EfTranslationRepository>();
        services.AddScoped<IPolarCatalogReader, EfPolarCatalogReader>();
        services.AddSingleton<IPolarCatalogTranslationCache, MemoryPolarCatalogTranslationCache>();
        return services;
    }
}
