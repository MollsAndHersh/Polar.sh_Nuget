namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// Query parameters for <see cref="IStorefrontCatalogProvider.ListProductsAsync"/>.
/// </summary>
/// <remarks>
/// Acts as a filter / sort / paging envelope; the catalog provider is free to translate
/// these into whatever its backing store supports (SQL, Meilisearch, etc.).
/// </remarks>
public sealed record ListProductsQuery
{
    /// <summary>Optional category filter; products in any of the listed categories match.</summary>
    public IReadOnlyList<string> CategoryIds { get; init; } = [];

    /// <summary>Optional tag filter; products tagged with any listed tag match.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Optional free-text fragment matched against product name and description.
    /// Catalog providers may treat this as a case-insensitive substring or hand it
    /// to a search backend; see <c>IStorefrontSearchProvider</c> for the dedicated
    /// search surface.
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>Optional language tag (BCP-47) used to select localized strings.</summary>
    public string? Language { get; init; }

    /// <summary>0-based page index.</summary>
    public int Page { get; init; }

    /// <summary>Rows per page; catalog providers cap at an implementation-defined ceiling.</summary>
    public int PageSize { get; init; } = 24;

    /// <summary>Sort token; recognised values are catalog-provider-specific (for example <c>"name"</c>, <c>"price_asc"</c>).</summary>
    public string? SortBy { get; init; }
}
