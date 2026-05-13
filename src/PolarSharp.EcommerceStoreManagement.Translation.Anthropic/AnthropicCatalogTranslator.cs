using System.Net.Http.Json;
using System.Text.Json;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.Anthropic;

/// <summary>
/// Anthropic Claude implementation of <see cref="IPolarCatalogTranslator"/>. Posts to
/// <c>https://api.anthropic.com/v1/messages</c> with a strict instruction to return only a
/// JSON object mapping the source field names to translated values.
/// </summary>
/// <remarks>
/// <para>Uses raw <see cref="HttpClient"/> + <see cref="JsonSerializer"/> — no third-party SDK
/// dependency. The Anthropic Messages API contract is stable and AOT-safe under this
/// approach.</para>
/// </remarks>
public sealed class AnthropicCatalogTranslator : IPolarCatalogTranslator
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    /// <summary>Initializes the translator with a pre-configured <see cref="HttpClient"/>.</summary>
    public AnthropicCatalogTranslator(HttpClient http, string apiKey, string model)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        _http = http;
        _apiKey = apiKey;
        _model = model;
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

        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                model = _model,
                max_tokens = 2048,
                messages = new[]
                {
                    new { role = "user", content = prompt },
                },
            }, options: Json),
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        // Anthropic messages response shape: { "content": [ { "type": "text", "text": "..." } ] }
        var text = doc.RootElement
            .GetProperty("content")
            .EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "text")
            .Select(e => e.GetProperty("text").GetString())
            .FirstOrDefault() ?? string.Empty;

        return TranslationPrompt.ParseJsonResponse(text, sourceFields.Keys);
    }
}
