using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks.BackgroundQueue;

/// <summary>
/// A bounded channel that buffers webhook events for asynchronous processing by a background
/// <see cref="PolarWebhookBackgroundService{TEvent}"/>.
/// </summary>
/// <typeparam name="TEvent">The webhook event type this queue buffers.</typeparam>
/// <remarks>
/// <para>
/// Opt-in via the <c>enqueue: true</c> parameter on
/// <c>AddWebhookHandler&lt;TEvent, THandler&gt;(enqueue: true)</c>. When enabled,
/// the webhook endpoint returns HTTP 200 immediately after writing the event to the channel;
/// the handler runs asynchronously in <see cref="PolarWebhookBackgroundService{TEvent}"/>.
/// </para>
/// <para>
/// This pattern prevents Polar's 30-second delivery timeout from triggering spurious retries
/// for handlers that require more than a few seconds (database migrations, external API calls,
/// email delivery, etc.).
/// </para>
/// <para>
/// On graceful shutdown, the background service drains remaining events up to
/// <see cref="PolarWebhookOptions.GracefulDrainTimeoutSeconds"/> before the process exits.
/// Events still in the queue after the timeout are dropped and logged as warnings.
/// </para>
/// </remarks>
public interface IBackgroundPolarWebhookQueue<TEvent> where TEvent : WebhookEvent
{
    /// <summary>
    /// Attempts to write the event to the queue without blocking.
    /// </summary>
    /// <param name="event">The verified, deserialized webhook event.</param>
    /// <returns>
    /// <see langword="true"/> if the event was accepted; <see langword="false"/> when the
    /// channel is full (the endpoint will return HTTP 429 in that case).
    /// </returns>
    bool TryEnqueue(TEvent @event);

    /// <summary>
    /// Reads the next queued event, waiting until one is available or the token is cancelled.
    /// </summary>
    /// <param name="ct">A cancellation token that aborts the wait.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that resolves to the next event.
    /// </returns>
    ValueTask<TEvent> DequeueAsync(CancellationToken ct);

    /// <summary>Gets the number of events currently buffered in the queue.</summary>
    int Count { get; }
}
