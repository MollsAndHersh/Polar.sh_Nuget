using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.OpenAI;

/// <summary>
/// OpenAI implementation of <see cref="IPolarCatalogTranslator"/>. Posts to OpenAI's
/// <c>/v1/chat/completions</c> (or a configurable compatible endpoint — that's how the
/// Grok package reuses this same code path).
/// </summary>
public sealed class OpenAiCatalogTranslator : IPolarCatalogTranslator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;

    /// <summary>Initializes the translator. <paramref name="endpoint"/> defaults to OpenAI's public API; override for OpenAI-compatible providers.</summary>
    public OpenAiCatalogTranslator(HttpClient http, string apiKey, string model, string endpoint = OpenAiTranslatorDefaults.Endpoint)
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
        var url = $"{_endpoint.TrimEnd('/')}/chat/completions";

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                model = _model,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = "You translate product catalog fields. Return strict JSON only." },
                    new { role = "user", content = prompt },
                },
            }, options: Json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        // OpenAI response shape: { "choices": [ { "message": { "content": "..." } } ] }
        var content = doc.RootElement
            .GetProperty("choices")
            .EnumerateArray()
            .Select(c => c.GetProperty("message").GetProperty("content").GetString())
            .FirstOrDefault() ?? string.Empty;

        return TranslationPrompt.ParseJsonResponse(content, sourceFields.Keys);
    }
}

/// <summary>Default values for the OpenAI provider.</summary>
public static class OpenAiTranslatorDefaults
{
    /// <summary>OpenAI's public API endpoint.</summary>
    public const string Endpoint = "https://api.openai.com/v1";
    /// <summary>Default model — gpt-4o-mini balances cost and translation quality.</summary>
    public const string Model = "gpt-4o-mini";
}
