using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement;

/// <summary>
/// A locally-authored Polar checkout-link configuration. Maps to one Polar
/// <c>CheckoutLink</c> on publish — the resulting URL is what the host embeds in their
/// storefront / email / sales page.
/// </summary>
public sealed record LocalCheckoutLinkConfig : ITenantOwned, IFakeDataAware
{
    /// <summary>Local identifier for the checkout link.</summary>
    public required CheckoutLinkId Id { get; init; }

    /// <inheritdoc/>
    public required string TenantId { get; init; }

    /// <inheritdoc/>
    public bool IsFakeData { get; init; }

    /// <summary>Host-side display label.</summary>
    public required string Name { get; init; }

    /// <summary>The products this link sells. Polar supports multi-product links — first product is treated as primary.</summary>
    public required IReadOnlyList<ProductId> ProductIds { get; init; }

    /// <summary>HTTPS URL Polar redirects the customer to on successful purchase.</summary>
    public string? SuccessUrl { get; init; }

    /// <summary>HTTPS URL Polar redirects to on cancellation.</summary>
    public string? CancelUrl { get; init; }

    /// <summary>Hex color (e.g. <c>"#0066cc"</c>) applied to the Polar checkout UI.</summary>
    public string? ThemeColor { get; init; }

    /// <summary>Public URL of a logo image displayed on the checkout page.</summary>
    public string? LogoUrl { get; init; }

    /// <summary>Optional structured fields collected from the customer during checkout.</summary>
    public IReadOnlyList<CheckoutCustomField> CustomFields { get; init; } = [];

    /// <summary>When true, the checkout UI accepts coupon-code entry.</summary>
    public bool AllowDiscountCodes { get; init; } = true;

    /// <summary>When true, the customer's billing address is required to complete checkout.</summary>
    public bool RequireBillingAddress { get; init; }

    /// <summary>The Polar checkout-link id assigned on first publish.</summary>
    public string? PolarCheckoutLinkId { get; init; }

    /// <summary>Current publish status.</summary>
    public PublishStatus Status { get; init; } = PublishStatus.Draft;
}

/// <summary>One structured field collected from the customer during checkout.</summary>
/// <param name="Key">Programmatic key — appears in the resulting Order's metadata.</param>
/// <param name="Label">Customer-facing label.</param>
/// <param name="Kind">Input type.</param>
/// <param name="Required">When true, the customer cannot submit without filling this field.</param>
public sealed record CheckoutCustomField(string Key, string Label, CustomFieldKind Kind, bool Required);
