using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.Gemini;

/// <summary>Builds a <see cref="GeminiCatalogTranslator"/> from a credentials tuple.</summary>
internal sealed class GeminiTranslatorFactory : IPolarCatalogTranslatorFactory
{
    private readonly IHttpClientFactory _httpFactory;

    public GeminiTranslatorFactory(IHttpClientFactory httpFactory)
    {
        ArgumentNullException.ThrowIfNull(httpFactory);
        _httpFactory = httpFactory;
    }

    /// <inheritdoc/>
    public TranslationProvider Provider => TranslationProvider.Gemini;

    /// <inheritdoc/>
    public IPolarCatalogTranslator Create(string apiKey, string? model, string? endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var http = _httpFactory.CreateClient("PolarSharp.Translation.Gemini");
        return new GeminiCatalogTranslator(
            http,
            apiKey,
            model ?? GeminiTranslatorDefaults.Model,
            string.IsNullOrWhiteSpace(endpoint) ? GeminiTranslatorDefaults.Endpoint : endpoint);
    }
}
