using Microsoft.AspNetCore.DataProtection;

namespace PolarSharp.EcommerceStoreManagement.Translation;

/// <summary>
/// Implements the documented 3-tier translation provider resolution:
/// </summary>
/// <remarks>
/// <list type="number">
///   <item><description><strong>Per-tenant</strong> — when the tenant's
///   <see cref="TenantBusinessProfile.TranslationProvider"/> is not
///   <see cref="TranslationProvider.None"/>, decrypt their stored API key and use that
///   provider.</description></item>
///   <item><description><strong>Master / SaaS-site</strong> — fall back to the global
///   <see cref="EcommerceTranslationMasterOptions"/> bound from
///   <c>PolarSharp:EcommerceStoreManagement:Translation</c>.</description></item>
///   <item><description><strong>Disabled</strong> — neither tier produces a translator;
///   the resolver returns <see langword="null"/> and translation features gracefully
///   no-op for that tenant.</description></item>
/// </list>
/// </remarks>
public interface ITranslationProviderResolver
{
    /// <summary>Returns the effective translator for the current tenant, or <see langword="null"/> when translation is disabled (Tier 3).</summary>
    Task<IPolarCatalogTranslator?> ResolveAsync(CancellationToken ct = default);
}

/// <summary>The master / SaaS-site translation provider configuration. Bound from <c>PolarSharp:EcommerceStoreManagement:Translation</c>.</summary>
public sealed class EcommerceTranslationMasterOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "PolarSharp:EcommerceStoreManagement:Translation";

    /// <summary>The master provider. <see cref="TranslationProvider.None"/> disables Tier 2.</summary>
    public TranslationProvider Provider { get; set; } = TranslationProvider.None;

    /// <summary>Master API key. <see langword="null"/> disables Tier 2.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Provider-specific model.</summary>
    public string? Model { get; set; }

    /// <summary>Endpoint URL — required for Azure OpenAI.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Maximum concurrent translator requests across all tenants using the master provider.</summary>
    public int MaxConcurrentTranslations { get; set; } = 4;

    /// <summary>Languages the master provider supports. The host's UI surfaces this list.</summary>
    public IReadOnlyList<string> SupportedLanguages { get; set; } = ["en-US", "es-MX", "fr-FR", "de-DE"];
}

/// <summary>
/// Factory the resolver uses to construct a translator from a (Provider, ApiKey, Model,
/// Endpoint) tuple. Each provider package registers its own factory; the resolver matches by
/// <see cref="TranslationProvider"/> discriminator.
/// </summary>
public interface IPolarCatalogTranslatorFactory
{
    /// <summary>The provider this factory handles.</summary>
    TranslationProvider Provider { get; }

    /// <summary>Builds a translator instance from the supplied credentials.</summary>
    /// <param name="apiKey">Plaintext API key. The caller has already decrypted any per-tenant ciphertext.</param>
    /// <param name="model">Provider-specific model name.</param>
    /// <param name="endpoint">Optional endpoint override (Azure OpenAI / Grok).</param>
    IPolarCatalogTranslator Create(string apiKey, string? model, string? endpoint);
}

/// <summary>The Data Protection purpose string used to encrypt per-tenant translation API keys.</summary>
public static class TranslationApiKeyProtection
{
    /// <summary>Data Protection purpose — uniquely binds protected blobs to this feature.</summary>
    public const string Purpose = "PolarSharp.EcommerceStoreManagement.TranslationApiKey";

    /// <summary>Returns a protector pre-configured for translation API key crypto.</summary>
    public static IDataProtector ForTranslationApiKey(this IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.CreateProtector(Purpose);
    }
}
