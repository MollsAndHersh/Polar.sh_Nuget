using System.Net.Http.Json;
using System.Text.Json;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI;

/// <summary>
/// Azure OpenAI implementation of <see cref="IPolarCatalogTranslator"/>. Posts to
/// <c>{endpoint}/openai/deployments/{deployment}/chat/completions?api-version=...</c>.
/// Authentication via the <c>api-key</c> header (Azure's convention; key auth, no Bearer).
/// </summary>
public sealed class AzureOpenAiCatalogTranslator : IPolarCatalogTranslator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _deployment;
    private readonly string _endpoint;
    private readonly string _apiVersion;

    /// <summary>Initializes the translator.</summary>
    /// <param name="http">Pre-configured HttpClient.</param>
    /// <param name="apiKey">Azure OpenAI key.</param>
    /// <param name="deployment">Your deployment name (acts as the model identifier in Azure OpenAI).</param>
    /// <param name="endpoint">Your Azure OpenAI resource endpoint, e.g. <c>https://my-resource.openai.azure.com</c>.</param>
    /// <param name="apiVersion">Azure OpenAI API version (default: <c>2024-10-21</c>).</param>
    public AzureOpenAiCatalogTranslator(
        HttpClient http,
        string apiKey,
        string deployment,
        string endpoint,
        string apiVersion = AzureOpenAiTranslatorDefaults.ApiVersion)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deployment);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiVersion);
        _http = http;
        _apiKey = apiKey;
        _deployment = deployment;
        _endpoint = endpoint;
        _apiVersion = apiVersion;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> TranslateAsync(
        IReadOnlyDictionary<string, string> sourceFields,
        string masterLanguage,
        string targetLanguage,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFields);
        if (sourceFields.Count == 0) return new Dictionary<string, string>();

        var prompt = TranslationPrompt.Build(sourceFields, masterLanguage, targetLanguage);
        var url = $"{_endpoint.TrimEnd('/')}/openai/deployments/{_deployment}/chat/completions?api-version={_apiVersion}";

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = "You translate product catalog fields. Return strict JSON only." },
                    new { role = "user", content = prompt },
                },
            }, options: Json),
        };
        request.Headers.Add("api-key", _apiKey);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var content = doc.RootElement
            .GetProperty("choices")
            .EnumerateArray()
            .Select(c => c.GetProperty("message").GetProperty("content").GetString())
            .FirstOrDefault() ?? string.Empty;

        return TranslationPrompt.ParseJsonResponse(content, sourceFields.Keys);
    }
}

/// <summary>Default values for the Azure OpenAI provider.</summary>
public static class AzureOpenAiTranslatorDefaults
{
    /// <summary>Default Azure OpenAI API version.</summary>
    public const string ApiVersion = "2024-10-21";
}
