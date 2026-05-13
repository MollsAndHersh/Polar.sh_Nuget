using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.OpenAI;

/// <summary>Builds an <see cref="OpenAiCatalogTranslator"/> from a credentials tuple.</summary>
internal sealed class OpenAiTranslatorFactory : IPolarCatalogTranslatorFactory
{
    private readonly IHttpClientFactory _httpFactory;

    public OpenAiTranslatorFactory(IHttpClientFactory httpFactory)
    {
        ArgumentNullException.ThrowIfNull(httpFactory);
        _httpFactory = httpFactory;
    }

    /// <inheritdoc/>
    public TranslationProvider Provider => TranslationProvider.OpenAI;

    /// <inheritdoc/>
    public IPolarCatalogTranslator Create(string apiKey, string? model, string? endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var http = _httpFactory.CreateClient("PolarSharp.Translation.OpenAI");
        return new OpenAiCatalogTranslator(
            http,
            apiKey,
            model ?? OpenAiTranslatorDefaults.Model,
            string.IsNullOrWhiteSpace(endpoint) ? OpenAiTranslatorDefaults.Endpoint : endpoint);
    }
}
