namespace PolarSharp.EcommerceStorefronts.Abstractions.Search;

/// <summary>Faceted-search aggregations for the current query.</summary>
/// <remarks>
/// Renders the left-rail facet panel: each entry in <see cref="Facets"/> is one facet
/// (brand, size, colour, price-bucket) and its selectable values.
/// </remarks>
public sealed record SearchFacets
{
    /// <summary>Facet name → ordered list of selectable values with counts.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<SearchFacetValue>> Facets { get; init; }
}
