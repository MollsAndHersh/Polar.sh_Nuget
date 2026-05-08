using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks.BackgroundQueue;

/// <summary>
/// Background service that drains <see cref="IBackgroundPolarWebhookQueue{TEvent}"/> and
/// invokes the registered <see cref="IPolarWebhookHandler{TEvent}"/>.
/// </summary>
/// <typeparam name="TEvent">The webhook event type this service processes.</typeparam>
/// <remarks>
/// <para>
/// Registered as an <see cref="IHostedService"/> when
/// <c>AddWebhookHandler&lt;TEvent, THandler&gt;(enqueue: true)</c> is called.
/// </para>
/// <para>
/// On graceful shutdown (<see cref="IHostApplicationLifetime.ApplicationStopping"/>),
/// this service drains any remaining events from the queue up to the drain timeout
/// configured by <see cref="PolarWebhookOptions.GracefulDrainTimeoutSeconds"/>.
/// Events still in the queue after the timeout are logged as warnings and dropped.
/// </para>
/// </remarks>
public sealed class PolarWebhookBackgroundService<TEvent>(
    IBackgroundPolarWebhookQueue<TEvent> queue,
    IPolarWebhookHandler<TEvent> handler,
    IOptionsMonitor<PolarWebhookOptions> options,
    ILogger<PolarWebhookBackgroundService<TEvent>> logger) : BackgroundService
    where TEvent : WebhookEvent
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "PolarSharp background webhook queue started for {EventType}.",
            typeof(TEvent).Name);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TEvent @event;
                try
                {
                    @event = await queue.DequeueAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await ProcessSafeAsync(@event, stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await DrainAsync().ConfigureAwait(false);
        }
    }

    private async Task ProcessSafeAsync(TEvent @event, CancellationToken ct)
    {
        try
        {
            await handler.HandleAsync(@event, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Unhandled exception in background webhook handler for {EventType} {WebhookId}.",
                @event.Type, @event.WebhookId);
        }
    }

    private async Task DrainAsync()
    {
        if (queue.Count == 0) return;

        var drainSeconds = options.CurrentValue.GracefulDrainTimeoutSeconds;
        using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(drainSeconds));

        logger.LogInformation(
            "PolarSharp background queue draining {Count} remaining {EventType} events " +
            "(timeout: {DrainSeconds}s).",
            queue.Count, typeof(TEvent).Name, drainSeconds);

        try
        {
            while (queue.Count > 0 && !drainCts.IsCancellationRequested)
            {
                var @event = await queue.DequeueAsync(drainCts.Token).ConfigureAwait(false);
                await ProcessSafeAsync(@event, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "PolarSharp background queue drain timed out after {DrainSeconds}s; " +
                "{Count} {EventType} events were dropped.",
                drainSeconds, queue.Count, typeof(TEvent).Name);
        }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Signal no more items will be written before base.StopAsync cancels ExecuteAsync.
        if (queue is PolarWebhookBackgroundQueue<TEvent> q)
            q.Complete();

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
