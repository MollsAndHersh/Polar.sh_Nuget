using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.Grok;

/// <summary>Builds a <see cref="GrokCatalogTranslator"/> from a credentials tuple.</summary>
internal sealed class GrokTranslatorFactory : IPolarCatalogTranslatorFactory
{
    private readonly IHttpClientFactory _httpFactory;

    public GrokTranslatorFactory(IHttpClientFactory httpFactory)
    {
        ArgumentNullException.ThrowIfNull(httpFactory);
        _httpFactory = httpFactory;
    }

    /// <inheritdoc/>
    public TranslationProvider Provider => TranslationProvider.Grok;

    /// <inheritdoc/>
    public IPolarCatalogTranslator Create(string apiKey, string? model, string? endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var http = _httpFactory.CreateClient("PolarSharp.Translation.Grok");
        return new GrokCatalogTranslator(
            http,
            apiKey,
            model ?? GrokTranslatorDefaults.Model,
            string.IsNullOrWhiteSpace(endpoint) ? GrokTranslatorDefaults.Endpoint : endpoint);
    }
}
