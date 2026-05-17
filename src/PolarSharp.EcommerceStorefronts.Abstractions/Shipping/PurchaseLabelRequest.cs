namespace PolarSharp.EcommerceStorefronts.Abstractions.Shipping;

/// <summary>Request to purchase a shipping label from a previously quoted rate.</summary>
public sealed record PurchaseLabelRequest
{
    /// <summary>The <see cref="ShippingRate.Id"/> the customer (or pipeline) selected.</summary>
    public required string RateId { get; init; }

    /// <summary>The order identifier the label is for; included for provider-side correlation.</summary>
    public required string OrderId { get; init; }

    /// <summary>
    /// Optional idempotency token; identical token short-circuits to the existing label
    /// so retries do not produce duplicate carrier charges.
    /// </summary>
    public string? IdempotencyToken { get; init; }
}
