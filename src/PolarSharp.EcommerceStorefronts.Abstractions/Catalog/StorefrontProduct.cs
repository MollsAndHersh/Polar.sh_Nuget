namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// A product as it appears in the storefront — name, marketing copy, media,
/// price + variants. Returned by <see cref="IStorefrontCatalogProvider"/>.
/// </summary>
/// <remarks>
/// Distinct from <c>PolarSharp.BaseEntities.PolarProductBase</c>: that type models the
/// authoring side of a product (the catalog the merchant maintains), while this type
/// is the read-side projection the storefront renders to customers.
/// </remarks>
public sealed record StorefrontProduct
{
    /// <summary>The product's identifier in the catalog provider.</summary>
    public required string Id { get; init; }

    /// <summary>The URL-safe slug for product detail page routing.</summary>
    public required string Slug { get; init; }

    /// <summary>The product's display name, localized when available.</summary>
    public required string Name { get; init; }

    /// <summary>Short marketing description suitable for product cards.</summary>
    public string? ShortDescription { get; init; }

    /// <summary>Long marketing description suitable for product detail pages.</summary>
    public string? LongDescription { get; init; }

    /// <summary>
    /// The product's default price (typically the lowest variant price). Variant
    /// prices override at the line-item level.
    /// </summary>
    public required StorefrontPrice DefaultPrice { get; init; }

    /// <summary>The product's purchasable variants. Empty for single-SKU products.</summary>
    public IReadOnlyList<StorefrontProductVariant> Variants { get; init; } = [];

    /// <summary>Media attached to the product (images, videos).</summary>
    public IReadOnlyList<StorefrontMedia> Media { get; init; } = [];

    /// <summary>Category identifiers the product belongs to.</summary>
    public IReadOnlyList<string> CategoryIds { get; init; } = [];

    /// <summary>Tag tokens for search and filtering.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>True when the product is in stock at the product (any variant) level.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>UTC timestamp the product was first published to the storefront.</summary>
    public DateTimeOffset? PublishedAt { get; init; }
}
