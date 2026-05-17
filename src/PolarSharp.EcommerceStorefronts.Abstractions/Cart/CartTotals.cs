namespace PolarSharp.EcommerceStorefronts.Abstractions.Cart;

/// <summary>Computed totals for a <see cref="Cart"/>; populated server-side on every mutation.</summary>
/// <remarks>
/// Tax and shipping totals are <see langword="null"/> until the cart has a shipping
/// address and the relevant checkout stages have run. The grand total is recomputed
/// every time a line item changes.
/// </remarks>
public sealed record CartTotals
{
    /// <summary>Sum of line subtotals in minor units (before discount, tax, shipping).</summary>
    public required int SubtotalCents { get; init; }

    /// <summary>Total discount applied in minor units (zero when no discount).</summary>
    public int DiscountCents { get; init; }

    /// <summary>Computed tax in minor units; <see langword="null"/> until the tax stage has quoted.</summary>
    public int? TaxCents { get; init; }

    /// <summary>Computed shipping in minor units; <see langword="null"/> until a rate is selected.</summary>
    public int? ShippingCents { get; init; }

    /// <summary>
    /// Grand total in minor units —
    /// <c>Subtotal - Discount + (Tax ?? 0) + (Shipping ?? 0)</c>.
    /// </summary>
    public required int GrandTotalCents { get; init; }

    /// <summary>ISO 4217 currency code; cart totals are single-currency.</summary>
    public required string Currency { get; init; }
}
