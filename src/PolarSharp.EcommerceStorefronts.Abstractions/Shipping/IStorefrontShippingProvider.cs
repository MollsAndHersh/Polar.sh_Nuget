namespace PolarSharp.EcommerceStorefronts.Abstractions.Shipping;

/// <summary>
/// Shipping provider abstraction. Implementations bridge to Shippo, EasyPost, or a
/// host's own carrier integration.
/// </summary>
/// <remarks>
/// Called by the <c>QuoteShipping</c> checkout pipeline stage during checkout, and
/// from <c>Fulfill</c> when the pipeline purchases a label.
/// </remarks>
public interface IStorefrontShippingProvider
{
    /// <summary>Fetches rate quotes for the requested shipment.</summary>
    /// <param name="request">Shipment parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Available rates.</returns>
    Task<StorefrontResult<IReadOnlyList<ShippingRate>>> GetRatesAsync(
        GetRatesRequest request,
        CancellationToken ct);

    /// <summary>Purchases a label for a previously quoted rate.</summary>
    /// <param name="request">The purchase request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The purchased label.</returns>
    Task<StorefrontResult<ShippingLabel>> PurchaseLabelAsync(
        PurchaseLabelRequest request,
        CancellationToken ct);

    /// <summary>Fetches the carrier-reported tracking status for a shipment.</summary>
    /// <param name="trackingNumber">The carrier's tracking number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Current tracking state.</returns>
    Task<StorefrontResult<ShipmentTracking>> GetTrackingAsync(
        string trackingNumber,
        CancellationToken ct);
}
