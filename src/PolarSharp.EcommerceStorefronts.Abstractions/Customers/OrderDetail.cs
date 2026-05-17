using PolarSharp.EcommerceStorefronts.Abstractions.Cart;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Customers;

/// <summary>The full order view shown on the customer-facing order detail page.</summary>
public sealed record OrderDetail
{
    /// <summary>The Polar order identifier.</summary>
    public required string OrderId { get; init; }

    /// <summary>Human-readable order number.</summary>
    public required string OrderNumber { get; init; }

    /// <summary>The Polar wire-format order status.</summary>
    public required string Status { get; init; }

    /// <summary>Line items on the order.</summary>
    public required IReadOnlyList<OrderLineItem> LineItems { get; init; }

    /// <summary>Subtotal in minor units (sum of line totals before tax + shipping).</summary>
    public required int SubtotalCents { get; init; }

    /// <summary>Discount applied to the order in minor units.</summary>
    public int DiscountCents { get; init; }

    /// <summary>Tax in minor units.</summary>
    public int TaxCents { get; init; }

    /// <summary>Shipping in minor units.</summary>
    public int ShippingCents { get; init; }

    /// <summary>Grand total in minor units.</summary>
    public required int TotalCents { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>Shipping address used for the order.</summary>
    public ShippingAddress? ShippingAddress { get; init; }

    /// <summary>URL to the hosted PDF invoice.</summary>
    public string? InvoiceUrl { get; init; }

    /// <summary>UTC timestamp the order was placed.</summary>
    public required DateTimeOffset PlacedAt { get; init; }

    /// <summary>UTC timestamp the order shipped (or digital fulfillment delivered).</summary>
    public DateTimeOffset? ShippedAt { get; init; }
}
