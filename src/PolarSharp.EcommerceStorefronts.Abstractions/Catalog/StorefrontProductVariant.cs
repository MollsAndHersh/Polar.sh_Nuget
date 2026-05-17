namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// One purchasable variant of a <see cref="StorefrontProduct"/> — for example a
/// specific size + colour of a t-shirt or a specific seat tier of a SaaS plan.
/// </summary>
/// <remarks>
/// Variants carry their own price + inventory + SKU; the parent product describes the
/// family-level marketing copy and shared media.
/// </remarks>
public sealed record StorefrontProductVariant
{
    /// <summary>The variant's identifier in the catalog provider.</summary>
    public required string Id { get; init; }

    /// <summary>Stock-keeping unit code, when the variant has one.</summary>
    public string? Sku { get; init; }

    /// <summary>The variant's display name (localized when available).</summary>
    public required string Name { get; init; }

    /// <summary>The variant's price tag.</summary>
    public required StorefrontPrice Price { get; init; }

    /// <summary>
    /// Available inventory; <see langword="null"/> when the variant is not stock-tracked
    /// (digital goods, services, infinite-supply benefits).
    /// </summary>
    public int? InventoryAvailable { get; init; }

    /// <summary>
    /// Attributes selected by this variant (for example <c>{ "size": "L", "colour": "blue" }</c>).
    /// Drives the variant-picker UI on the product detail page.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; }
        = new Dictionary<string, string>();

    /// <summary>Variant-specific media (overrides parent product media when present).</summary>
    public IReadOnlyList<StorefrontMedia> Media { get; init; } = [];
}
