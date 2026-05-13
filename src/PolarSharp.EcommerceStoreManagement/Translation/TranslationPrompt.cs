using System.Text;
using System.Text.Json;

namespace PolarSharp.EcommerceStoreManagement.Translation;

/// <summary>
/// Shared helpers for translation provider packages: builds the standard prompt every
/// translator sends to its AI backend, and parses the strict-JSON response back into a
/// field-name → translation dictionary.
/// </summary>
/// <remarks>
/// <para>
/// All five provider packages (Anthropic / OpenAI / Azure OpenAI / Gemini / Grok) use the
/// same prompt shape — keeps translation quality consistent across providers and lets the
/// host A/B test providers without per-provider prompt tweaks.
/// </para>
/// </remarks>
public static class TranslationPrompt
{
    /// <summary>
    /// Builds the user-facing prompt that instructs the AI to translate the supplied fields
    /// and return ONLY a JSON object (no prose, no markdown fence).
    /// </summary>
    /// <param name="sourceFields">Field-name → master-language value.</param>
    /// <param name="masterLanguage">The source language code (e.g. <c>"en-US"</c>).</param>
    /// <param name="targetLanguage">The destination language code (e.g. <c>"es-MX"</c>).</param>
    public static string Build(
        IReadOnlyDictionary<string, string> sourceFields,
        string masterLanguage,
        string targetLanguage)
    {
        ArgumentNullException.ThrowIfNull(sourceFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(masterLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);

        var sb = new StringBuilder();
        sb.Append("You are a professional product-catalog translator. Translate the following fields from ");
        sb.Append(masterLanguage);
        sb.Append(" to ");
        sb.Append(targetLanguage);
        sb.AppendLine(".");
        sb.AppendLine("Return ONLY a single valid JSON object whose keys are the same field names as the input and whose values are the translated strings. Do not include markdown fences, prose, or any commentary. Do not wrap the JSON in any other structure.");
        sb.AppendLine();
        sb.AppendLine("Input:");
        sb.Append(JsonSerializer.Serialize(sourceFields, new JsonSerializerOptions { WriteIndented = true }));
        return sb.ToString();
    }

    /// <summary>
    /// Parses the AI's JSON response back into a dictionary. Tolerates surrounding whitespace
    /// and accidental <c>```json</c> markdown fences. Filters keys to only those present in
    /// <paramref name="expectedFieldNames"/> so a hallucinated extra field is dropped silently.
    /// </summary>
    /// <param name="responseText">The AI's raw text response.</param>
    /// <param name="expectedFieldNames">The field names the caller asked to be translated.</param>
    public static IReadOnlyDictionary<string, string> ParseJsonResponse(
        string responseText,
        IEnumerable<string> expectedFieldNames)
    {
        ArgumentNullException.ThrowIfNull(expectedFieldNames);
        var expected = new HashSet<string>(expectedFieldNames, StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(responseText)) return result;

        var cleaned = StripMarkdownFences(responseText.Trim());

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!expected.Contains(property.Name)) continue;
                if (property.Value.ValueKind != JsonValueKind.String) continue;
                var value = property.Value.GetString();
                if (value is not null) result[property.Name] = value;
            }
        }
        catch (JsonException)
        {
            // The AI returned malformed JSON. Return whatever we've parsed so far (likely empty)
            // — the resolver caller treats partial translation as best-effort and falls back to
            // master-language values per-field.
        }

        return result;
    }

    private static string StripMarkdownFences(string text)
    {
        // Common failure: AI wraps response in ```json ... ``` despite the instruction.
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
        }
        else if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
        }
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }
}
