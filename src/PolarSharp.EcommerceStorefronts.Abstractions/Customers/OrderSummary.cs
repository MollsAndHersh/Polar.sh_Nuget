namespace PolarSharp.EcommerceStorefronts.Abstractions.Customers;

/// <summary>One row in the customer's order history list.</summary>
/// <remarks>
/// Pre-aggregated counts + amounts mean the order-list page renders without a
/// per-row drilldown query.
/// </remarks>
public sealed record OrderSummary
{
    /// <summary>The Polar order identifier.</summary>
    public required string OrderId { get; init; }

    /// <summary>Human-readable order number for support reference.</summary>
    public required string OrderNumber { get; init; }

    /// <summary>The Polar wire-format order status.</summary>
    public required string Status { get; init; }

    /// <summary>Order grand total in minor units.</summary>
    public required int TotalCents { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>Number of distinct line items.</summary>
    public required int LineItemCount { get; init; }

    /// <summary>UTC timestamp the order was placed.</summary>
    public required DateTimeOffset PlacedAt { get; init; }
}
