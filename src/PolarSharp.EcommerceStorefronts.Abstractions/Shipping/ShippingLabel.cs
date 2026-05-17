namespace PolarSharp.EcommerceStorefronts.Abstractions.Shipping;

/// <summary>A purchased shipping label.</summary>
public sealed record ShippingLabel
{
    /// <summary>The provider's label identifier.</summary>
    public required string Id { get; init; }

    /// <summary>The carrier-issued tracking number.</summary>
    public required string TrackingNumber { get; init; }

    /// <summary>URL of the printable label artifact (typically PDF or PNG).</summary>
    public required string LabelUrl { get; init; }

    /// <summary>The carrier the label is for.</summary>
    public required string Carrier { get; init; }

    /// <summary>The actual purchase price in minor units.</summary>
    public required int AmountCents { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>UTC timestamp the label was purchased.</summary>
    public required DateTimeOffset PurchasedAt { get; init; }
}
