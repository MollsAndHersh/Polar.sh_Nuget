namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>One media asset (image, video poster, thumbnail) attached to a storefront product.</summary>
/// <remarks>
/// The asset is referenced by absolute URL; the catalog provider is responsible for any
/// CDN signing / image resizing transforms before populating <see cref="Url"/>.
/// </remarks>
public sealed record StorefrontMedia
{
    /// <summary>The asset's identifier within the catalog provider.</summary>
    public required string Id { get; init; }

    /// <summary>The asset's absolute, publicly-accessible URL.</summary>
    public required string Url { get; init; }

    /// <summary>
    /// The asset's MIME type (for example <c>"image/webp"</c>, <c>"image/avif"</c>,
    /// <c>"video/mp4"</c>).
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>Alt text, localized to the requested language when available.</summary>
    public string? AltText { get; init; }

    /// <summary>Pixel width of the asset, when known.</summary>
    public int? Width { get; init; }

    /// <summary>Pixel height of the asset, when known.</summary>
    public int? Height { get; init; }

    /// <summary>0-based display order; lower values render first.</summary>
    public int SortOrder { get; init; }
}
