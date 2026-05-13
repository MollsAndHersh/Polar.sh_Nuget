namespace PolarSharp.EcommerceStoreManagement.Translation;

/// <summary>
/// Abstraction over the AI translation HTTP call. Each provider package
/// (<c>Translation.Anthropic</c>, <c>Translation.OpenAI</c>,
/// <c>Translation.AzureOpenAI</c>, <c>Translation.Gemini</c>, <c>Translation.Grok</c>)
/// supplies a concrete implementation registered as the master / tier-2 provider.
/// </summary>
/// <remarks>
/// The resolver invokes this; the resolver itself is the right surface for application code
/// to consume (which factors in the 3-tier per-tenant / master / disabled cascade).
/// </remarks>
public interface IPolarCatalogTranslator
{
    /// <summary>Translates a bag of field-name → text pairs from the master language to a single target language.</summary>
    /// <param name="sourceFields">Master-language values keyed by field name (e.g. <c>{"name": "Premium Headphones", "description": "..."}</c>).</param>
    /// <param name="masterLanguage">The source language code (e.g. <c>"en-US"</c>).</param>
    /// <param name="targetLanguage">The destination language code (e.g. <c>"es-MX"</c>).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>Translated values keyed by the same field names.</returns>
    Task<IReadOnlyDictionary<string, string>> TranslateAsync(
        IReadOnlyDictionary<string, string> sourceFields,
        string masterLanguage,
        string targetLanguage,
        CancellationToken ct = default);
}

/// <summary>Default no-op translator — returns the source text unchanged. Used when translation is disabled (Tier 3 in the 3-tier resolution).</summary>
public sealed class NoOpCatalogTranslator : IPolarCatalogTranslator
{
    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, string>> TranslateAsync(
        IReadOnlyDictionary<string, string> sourceFields,
        string masterLanguage,
        string targetLanguage,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFields);
        return Task.FromResult<IReadOnlyDictionary<string, string>>(sourceFields);
    }
}
