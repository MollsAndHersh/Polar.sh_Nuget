using PolarSharp.EcommerceStorefronts.Abstractions.Paging;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// Read-side projection of the merchant catalog as it is presented to storefront
/// customers. Implementations bridge to the authoritative catalog (Polar.Catalog,
/// a host CMS, or a search index) without ever mutating it.
/// </summary>
/// <remarks>
/// The storefront browses; it never authors. Mutating operations belong in
/// <c>PolarSharp.EcommerceStoreManagement</c>.
/// </remarks>
public interface IStorefrontCatalogProvider
{
    /// <summary>Lists products matching <paramref name="query"/>, page by page.</summary>
    /// <param name="query">Filter / sort / paging parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result of <see cref="StorefrontProduct"/> rows.</returns>
    Task<StorefrontResult<PagedResult<StorefrontProduct>>> ListProductsAsync(
        ListProductsQuery query,
        CancellationToken ct);

    /// <summary>Fetches one product by its identifier.</summary>
    /// <param name="productId">The catalog provider's product identifier.</param>
    /// <param name="language">Optional BCP-47 language tag for localized strings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The product, or a <see cref="StorefrontNotFoundError"/> when unknown.</returns>
    Task<StorefrontResult<StorefrontProduct>> GetProductAsync(
        string productId,
        string? language,
        CancellationToken ct);

    /// <summary>Lists every category in the catalog.</summary>
    /// <param name="language">Optional BCP-47 language tag for localized strings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The flat list of categories; nest by walking <c>ParentCategoryId</c>.</returns>
    Task<StorefrontResult<IReadOnlyList<StorefrontCategory>>> ListCategoriesAsync(
        string? language,
        CancellationToken ct);

    /// <summary>Returns the current merchant's customer-facing business profile.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The business profile.</returns>
    Task<StorefrontResult<StorefrontBusinessProfile>> GetBusinessProfileAsync(CancellationToken ct);
}
