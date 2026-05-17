using PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Cart;

/// <summary>One line in a <see cref="Cart"/> — a specific product/variant at a specific quantity.</summary>
/// <remarks>
/// Line totals are computed server-side. Client-supplied prices are ignored and
/// recomputed by the <c>ValidateLineItems</c> stage during checkout.
/// </remarks>
public sealed record CartLineItem
{
    /// <summary>Stable identifier for this line within the cart.</summary>
    public required string LineId { get; init; }

    /// <summary>The catalog provider's product identifier.</summary>
    public required string ProductId { get; init; }

    /// <summary>The catalog provider's variant identifier; <see langword="null"/> for single-SKU products.</summary>
    public string? VariantId { get; init; }

    /// <summary>Snapshot of the product name at add-to-cart time (for cart display continuity).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Snapshot of a single product/variant image suitable for the cart row.</summary>
    public StorefrontMedia? Thumbnail { get; init; }

    /// <summary>Quantity of the SKU on this line.</summary>
    public required int Quantity { get; init; }

    /// <summary>Unit price in minor units at add-to-cart time.</summary>
    public required int UnitAmountCents { get; init; }

    /// <summary>Line subtotal in minor units (<see cref="UnitAmountCents"/> × <see cref="Quantity"/>).</summary>
    public required int LineSubtotalCents { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>Per-line discount in minor units (zero when no discount applies).</summary>
    public int DiscountCents { get; init; }
}
