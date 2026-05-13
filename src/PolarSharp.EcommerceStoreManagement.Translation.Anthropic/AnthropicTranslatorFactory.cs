using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.Anthropic;

/// <summary>Builds an <see cref="AnthropicCatalogTranslator"/> from a credentials tuple.</summary>
internal sealed class AnthropicTranslatorFactory : IPolarCatalogTranslatorFactory
{
    private readonly IHttpClientFactory _httpFactory;

    public AnthropicTranslatorFactory(IHttpClientFactory httpFactory)
    {
        ArgumentNullException.ThrowIfNull(httpFactory);
        _httpFactory = httpFactory;
    }

    /// <inheritdoc/>
    public TranslationProvider Provider => TranslationProvider.Anthropic;

    /// <inheritdoc/>
    public IPolarCatalogTranslator Create(string apiKey, string? model, string? endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var http = _httpFactory.CreateClient("PolarSharp.Translation.Anthropic");
        return new AnthropicCatalogTranslator(http, apiKey, model ?? AnthropicTranslatorDefaults.Model);
    }
}

/// <summary>Default values for the Anthropic provider.</summary>
public static class AnthropicTranslatorDefaults
{
    /// <summary>Default model — Claude Sonnet 4.6 balances cost and quality for translation.</summary>
    public const string Model = "claude-sonnet-4-6";
}
