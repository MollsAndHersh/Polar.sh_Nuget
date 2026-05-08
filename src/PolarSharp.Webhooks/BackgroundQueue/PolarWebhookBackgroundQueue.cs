using System.Threading.Channels;
using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks.BackgroundQueue;

/// <summary>
/// Bounded channel implementation of <see cref="IBackgroundPolarWebhookQueue{TEvent}"/>.
/// </summary>
/// <typeparam name="TEvent">The webhook event type this queue buffers.</typeparam>
/// <remarks>
/// Uses <see cref="BoundedChannelOptions"/> with
/// <see cref="BoundedChannelFullMode.Wait"/> and a short timeout (100ms) so that a full
/// queue causes the webhook endpoint to return HTTP 429 rather than drop events silently.
/// Polar will retry the delivery rather than silently losing it.
/// </remarks>
internal sealed class PolarWebhookBackgroundQueue<TEvent> : IBackgroundPolarWebhookQueue<TEvent>
    where TEvent : WebhookEvent
{
    private readonly Channel<TEvent> _channel;

    /// <summary>
    /// Initializes a new queue with the given bounded capacity.
    /// </summary>
    /// <param name="capacity">
    /// Maximum number of events that can be buffered before <see cref="TryEnqueue"/> returns
    /// <see langword="false"/>. Set via
    /// <c>PolarSharp:Webhooks:BackgroundQueueCapacity</c> (default: 1000).
    /// </param>
    public PolarWebhookBackgroundQueue(int capacity)
    {
        _channel = Channel.CreateBounded<TEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <inheritdoc/>
    public bool TryEnqueue(TEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return _channel.Writer.TryWrite(@event);
    }

    /// <inheritdoc/>
    public ValueTask<TEvent> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);

    /// <inheritdoc/>
    public int Count => _channel.Reader.Count;

    /// <summary>Signals that no more items will be written to the queue.</summary>
    public void Complete() => _channel.Writer.Complete();
}
