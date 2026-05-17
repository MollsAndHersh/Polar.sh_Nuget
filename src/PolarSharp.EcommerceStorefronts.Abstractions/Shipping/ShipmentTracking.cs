namespace PolarSharp.EcommerceStorefronts.Abstractions.Shipping;

/// <summary>Carrier-reported tracking information for a shipment.</summary>
public sealed record ShipmentTracking
{
    /// <summary>The tracking number the carrier issued.</summary>
    public required string TrackingNumber { get; init; }

    /// <summary>The carrier (for example <c>"USPS"</c>).</summary>
    public required string Carrier { get; init; }

    /// <summary>A normalized status string (for example <c>"in_transit"</c>, <c>"delivered"</c>).</summary>
    public required string Status { get; init; }

    /// <summary>The carrier's tracking-page URL.</summary>
    public string? TrackingUrl { get; init; }

    /// <summary>Estimated delivery date (UTC); <see langword="null"/> when not provided.</summary>
    public DateTimeOffset? EstimatedDelivery { get; init; }

    /// <summary>UTC timestamp the tracking record was last refreshed.</summary>
    public required DateTimeOffset CheckedAt { get; init; }
}
