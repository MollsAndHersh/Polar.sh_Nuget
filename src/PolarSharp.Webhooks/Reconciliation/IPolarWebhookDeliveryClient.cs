namespace PolarSharp.Webhooks.Reconciliation;

/// <summary>
/// Abstraction over the Polar webhook deliveries API used by <see cref="PolarWebhookReconciler"/>
/// to fetch failed webhook deliveries for replay.
/// </summary>
/// <remarks>
/// <para>
/// The <c>PolarSharp</c> core package provides a concrete implementation backed by the
/// Kiota-generated Polar API client. Register it by calling
/// <c>AddPolarInfrastructure()</c> before <c>AddPolarWebhookReconciliation()</c>.
/// </para>
/// <para>
/// When running <c>PolarSharp.Webhooks</c> standalone (without the core package), no
/// implementation is registered and the reconciler logs a warning at startup then skips
/// reconciliation entirely. Implement this interface and register it with DI to enable
/// reconciliation in standalone deployments.
/// </para>
/// </remarks>
public interface IPolarWebhookDeliveryClient
{
    /// <summary>
    /// Returns a page of failed webhook deliveries newer than <paramref name="since"/>.
    /// </summary>
    /// <param name="since">
    /// The start of the time window. Only deliveries created after this timestamp are returned.
    /// </param>
    /// <param name="page">
    /// The one-based page number. The page size is implementation-defined (typically 100).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A read-only list of <see cref="ReconciliationDelivery"/> objects.
    /// Returns an empty list when there are no more pages.
    /// </returns>
    Task<IReadOnlyList<ReconciliationDelivery>> GetFailedDeliveriesAsync(
        DateTimeOffset since,
        int page,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A failed Polar webhook delivery returned by <see cref="IPolarWebhookDeliveryClient"/>.
/// </summary>
/// <param name="DeliveryId">
/// The unique identifier for this delivery attempt. Used for deduplication in the dispatcher.
/// </param>
/// <param name="PayloadJson">The raw JSON payload string of the webhook event.</param>
/// <param name="DeliveryTime">The UTC timestamp of this delivery attempt.</param>
public sealed record ReconciliationDelivery(
    string DeliveryId,
    string PayloadJson,
    DateTimeOffset DeliveryTime);
