using PolarSharp.EcommerceStorefronts.Abstractions.Catalog;
using PolarSharp.EcommerceStorefronts.Abstractions.Paging;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Search;

/// <summary>
/// Optional search index sitting in front of the catalog. Registered by bridges such
/// as <c>PolarSharp.EcommerceStorefronts.Search.MeiliSearch</c>; the storefront falls
/// back to <c>IStorefrontCatalogProvider</c> list+filter when no provider is wired.
/// </summary>
public interface IStorefrontSearchProvider
{
    /// <summary>Runs the search and returns paged products.</summary>
    /// <param name="query">Search parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of matching products.</returns>
    Task<StorefrontResult<PagedResult<StorefrontProduct>>> SearchAsync(
        SearchQuery query,
        CancellationToken ct);

    /// <summary>Returns facet aggregations for the current query.</summary>
    /// <param name="query">The query to aggregate against.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Facets keyed by facet name.</returns>
    Task<StorefrontResult<SearchFacets>> GetFacetsAsync(
        SearchQuery query,
        CancellationToken ct);

    /// <summary>Returns autocomplete suggestions for a partial query.</summary>
    /// <param name="partial">The partial text the customer has typed.</param>
    /// <param name="max">Maximum number of suggestions to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Suggestion strings.</returns>
    Task<StorefrontResult<IReadOnlyList<string>>> SuggestAsync(
        string partial,
        int max,
        CancellationToken ct);
}
