using System.Net.Http.Json;
using System.Text.Json;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.Gemini;

/// <summary>
/// Google Gemini implementation of <see cref="IPolarCatalogTranslator"/>. Posts to Gemini's
/// <c>generateContent</c> endpoint with the API key as a query parameter
/// (Gemini's auth convention).
/// </summary>
public sealed class GeminiCatalogTranslator : IPolarCatalogTranslator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;

    /// <summary>Initializes the translator.</summary>
    public GeminiCatalogTranslator(HttpClient http, string apiKey, string model, string endpoint = GeminiTranslatorDefaults.Endpoint)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        _http = http;
        _apiKey = apiKey;
        _model = model;
        _endpoint = endpoint;
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
        var url = $"{_endpoint.TrimEnd('/')}/v1beta/models/{_model}:generateContent?key={Uri.EscapeDataString(_apiKey)}";

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = prompt } },
                    },
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature = 0.2,
                },
            }, options: Json),
        };

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        // Gemini response shape:
        // { "candidates": [ { "content": { "parts": [ { "text": "..." } ] } } ] }
        var text = doc.RootElement
            .GetProperty("candidates")
            .EnumerateArray()
            .Select(c => c.GetProperty("content").GetProperty("parts").EnumerateArray().FirstOrDefault())
            .Where(p => p.ValueKind == JsonValueKind.Object)
            .Select(p => p.GetProperty("text").GetString())
            .FirstOrDefault() ?? string.Empty;

        return TranslationPrompt.ParseJsonResponse(text, sourceFields.Keys);
    }
}

/// <summary>Default values for the Gemini provider.</summary>
public static class GeminiTranslatorDefaults
{
    /// <summary>Google's generative-language API endpoint root.</summary>
    public const string Endpoint = "https://generativelanguage.googleapis.com";
    /// <summary>Default model — Gemini 2.5 Flash balances cost and translation quality.</summary>
    public const string Model = "gemini-2.5-flash";
}
