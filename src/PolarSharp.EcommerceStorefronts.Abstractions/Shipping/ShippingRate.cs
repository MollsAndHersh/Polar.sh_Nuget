namespace PolarSharp.EcommerceStorefronts.Abstractions.Shipping;

/// <summary>One shipping option returned by a shipping provider.</summary>
public sealed record ShippingRate
{
    /// <summary>The provider-specific rate identifier; quote this back when purchasing the label.</summary>
    public required string Id { get; init; }

    /// <summary>The shipping carrier (for example <c>"USPS"</c>, <c>"FedEx"</c>, <c>"DHL"</c>).</summary>
    public required string Carrier { get; init; }

    /// <summary>The service tier (for example <c>"Ground"</c>, <c>"Express 2-Day"</c>).</summary>
    public required string ServiceLevel { get; init; }

    /// <summary>The quoted shipping cost in minor units.</summary>
    public required int AmountCents { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>Estimated transit days; <see langword="null"/> when the carrier does not quote.</summary>
    public int? EstimatedDeliveryDays { get; init; }
}
