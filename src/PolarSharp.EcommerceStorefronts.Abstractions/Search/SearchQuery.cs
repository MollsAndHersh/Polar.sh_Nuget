namespace PolarSharp.EcommerceStorefronts.Abstractions.Search;

/// <summary>Parameters for a storefront search.</summary>
/// <remarks>
/// Search is optional; when no <c>IStorefrontSearchProvider</c> is registered the
/// storefront falls back to the catalog provider's list+filter implementation.
/// </remarks>
public sealed record SearchQuery
{
    /// <summary>The free-text query as entered by the customer.</summary>
    public required string Text { get; init; }

    /// <summary>Optional category restriction.</summary>
    public IReadOnlyList<string> CategoryIds { get; init; } = [];

    /// <summary>
    /// Selected facet filters: facet name → selected values (for example
    /// <c>{ "brand": ["Acme"], "size": ["M", "L"] }</c>).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Facets { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>BCP-47 language tag for ranking + localization.</summary>
    public string? Language { get; init; }

    /// <summary>0-based page index.</summary>
    public int Page { get; init; }

    /// <summary>Rows per page.</summary>
    public int PageSize { get; init; } = 24;

    /// <summary>Sort token; recognised values are provider-specific (for example <c>"relevance"</c>, <c>"price_asc"</c>).</summary>
    public string? SortBy { get; init; }
}
