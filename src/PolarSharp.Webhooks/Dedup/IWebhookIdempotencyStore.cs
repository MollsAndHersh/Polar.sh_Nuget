namespace PolarSharp.Webhooks.Dedup;

/// <summary>
/// Defines a store for tracking processed Polar webhook event IDs to prevent double-processing.
/// </summary>
/// <remarks>
/// Register a custom implementation to use Redis, SQL, or another distributed store.
/// The default in-memory implementation is registered by
/// <see cref="PolarSharp.Webhooks.Extensions.WebhookBuilderExtensions.AddPolarWebhookInMemoryDedup"/>.
/// </remarks>
public interface IWebhookIdempotencyStore
{
    /// <summary>
    /// Returns <see langword="true"/> if the webhook with the given ID has already been processed.
    /// </summary>
    /// <param name="webhookId">The Polar webhook delivery ID from the <c>webhook-id</c> header.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<bool> HasBeenProcessedAsync(string webhookId, CancellationToken ct = default);

    /// <summary>
    /// Marks the given webhook ID as processed.
    /// </summary>
    /// <param name="webhookId">The Polar webhook delivery ID to record.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask MarkProcessedAsync(string webhookId, CancellationToken ct = default);
}
