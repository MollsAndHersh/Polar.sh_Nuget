using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI;

/// <summary>Builds an <see cref="AzureOpenAiCatalogTranslator"/> from a credentials tuple. <c>model</c> is interpreted as the Azure OpenAI deployment name; <c>endpoint</c> is REQUIRED.</summary>
internal sealed class AzureOpenAiTranslatorFactory : IPolarCatalogTranslatorFactory
{
    private readonly IHttpClientFactory _httpFactory;

    public AzureOpenAiTranslatorFactory(IHttpClientFactory httpFactory)
    {
        ArgumentNullException.ThrowIfNull(httpFactory);
        _httpFactory = httpFactory;
    }

    /// <inheritdoc/>
    public TranslationProvider Provider => TranslationProvider.AzureOpenAI;

    /// <inheritdoc/>
    public IPolarCatalogTranslator Create(string apiKey, string? model, string? endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Azure OpenAI requires an Endpoint URL (your resource's https://{resource}.openai.azure.com).", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Azure OpenAI requires a Model value (your deployment name).", nameof(model));

        var http = _httpFactory.CreateClient("PolarSharp.Translation.AzureOpenAI");
        return new AzureOpenAiCatalogTranslator(http, apiKey, model, endpoint);
    }
}
