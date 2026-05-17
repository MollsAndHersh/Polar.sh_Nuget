namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>A storefront-visible product category, optionally nested under a parent.</summary>
/// <remarks>
/// Categories drive navigation (mega-menu, breadcrumbs) and filtering on product list
/// pages. Tree shape is recovered by walking <see cref="ParentCategoryId"/> chains.
/// </remarks>
public sealed record StorefrontCategory
{
    /// <summary>The category's identifier in the catalog provider.</summary>
    public required string Id { get; init; }

    /// <summary>The URL-safe slug for category page routing.</summary>
    public required string Slug { get; init; }

    /// <summary>The category's display name, localized when available.</summary>
    public required string Name { get; init; }

    /// <summary>Optional short description rendered on category landing pages.</summary>
    public string? Description { get; init; }

    /// <summary>Identifier of the parent category, or <see langword="null"/> at the root.</summary>
    public string? ParentCategoryId { get; init; }

    /// <summary>0-based sibling sort position; lower values render first.</summary>
    public int SortOrder { get; init; }

    /// <summary>Optional hero image for the category landing page.</summary>
    public StorefrontMedia? HeroImage { get; init; }
}
