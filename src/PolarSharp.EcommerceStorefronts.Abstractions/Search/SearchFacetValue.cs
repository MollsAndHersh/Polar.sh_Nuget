namespace PolarSharp.EcommerceStorefronts.Abstractions.Search;

/// <summary>One selectable facet value with its document count.</summary>
public sealed record SearchFacetValue
{
    /// <summary>The facet value (for example <c>"Acme"</c>).</summary>
    public required string Value { get; init; }

    /// <summary>Customer-friendly display label (defaults to <see cref="Value"/>).</summary>
    public string? DisplayLabel { get; init; }

    /// <summary>Number of products matching this facet value under the current query.</summary>
    public required int Count { get; init; }
}
