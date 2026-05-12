using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.Webhooks.Reconciliation;

/// <summary>
/// Periodically queries the Polar webhook deliveries API for failed deliveries and
/// replays them through the registered <see cref="IPolarWebhookDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered as an <see cref="IHostedService"/> by <c>AddPolarWebhookReconciliation()</c>.
/// Runs every <see cref="PolarReconciliationOptions.IntervalMinutes"/> minutes after startup.
/// </para>
/// <para>
/// Replayed events flow through the same <c>IPolarWebhookDispatcher</c> pipeline as live
/// webhooks. Handler implementations must be idempotent — the same event may arrive more
/// than once when reconciliation and live delivery overlap.
/// </para>
/// <para>
/// HMAC signature verification is skipped for replayed events because the data is sourced
/// directly from the authenticated Polar API, not from an inbound HTTP request.
/// </para>
/// <para>
/// Requires <see cref="IPolarWebhookDeliveryClient"/> to be registered in DI. When running
/// standalone (without the <c>PolarSharp</c> core package), implement
/// <see cref="IPolarWebhookDeliveryClient"/> and register it, or reconciliation will be
/// skipped with a startup warning.
/// </para>
/// </remarks>
internal sealed class PolarWebhookReconciler(
    IServiceScopeFactory scopeFactory,
    IOptions<PolarReconciliationOptions> options,
    IReconciliationCheckpointStore checkpoint,
    WebhookValidator validator,
    ILogger<PolarWebhookReconciler> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            logger.LogInformation("PolarSharp webhook reconciliation is disabled (Enabled=false).");
            return;
        }

        // Check if delivery client is available — required for reconciliation to function.
        await using var startupScope = scopeFactory.CreateAsyncScope();
        var deliveryClient = startupScope.ServiceProvider.GetService<IPolarWebhookDeliveryClient>();
        if (deliveryClient is null)
        {
            logger.LogWarning(
                "PolarSharp webhook reconciliation: No {ClientType} is registered. " +
                "Reconciliation requires the PolarSharp core package (AddPolarInfrastructure()) " +
                "or a custom IPolarWebhookDeliveryClient implementation. Reconciliation skipped.",
                nameof(IPolarWebhookDeliveryClient));
            return;
        }

        var interval = TimeSpan.FromMinutes(opts.IntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken).ConfigureAwait(false);

            try
            {
                await ReconcileAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PolarSharp reconciliation: unhandled error during reconciliation cycle.");
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        var opts = options.Value;
        var storedCheckpoint = await checkpoint.GetCheckpointAsync(ct).ConfigureAwait(false);
        var lowerBound = storedCheckpoint ?? DateTimeOffset.UtcNow.AddHours(-opts.MaxLookbackHours);
        var absoluteLowerBound = DateTimeOffset.UtcNow.AddHours(-opts.MaxLookbackHours);

        if (lowerBound < absoluteLowerBound)
            lowerBound = absoluteLowerBound;

        logger.LogInformation(
            "PolarSharp reconciliation: checking failed deliveries since {Since}.",
            lowerBound);

        await using var scope = scopeFactory.CreateAsyncScope();
        var deliveryClient = scope.ServiceProvider.GetRequiredService<IPolarWebhookDeliveryClient>();
        var dispatcher     = scope.ServiceProvider.GetRequiredService<IPolarWebhookDispatcher>();

        int page = 1;
        int replayed = 0;
        int skipped = 0;
        DateTimeOffset? latestProcessed = null;

        while (true)
        {
            var items = await deliveryClient.GetFailedDeliveriesAsync(lowerBound, page, ct)
                .ConfigureAwait(false);

            if (items.Count == 0)
                break;

            foreach (var delivery in items)
            {
                if (string.IsNullOrEmpty(delivery.PayloadJson))
                {
                    logger.LogWarning(
                        "PolarSharp reconciliation: delivery {DeliveryId} has no payload. Skipping.",
                        delivery.DeliveryId);
                    skipped++;
                    continue;
                }

                var deserializeResult = validator.Deserialize(
                    delivery.PayloadJson,
                    delivery.DeliveryId,
                    delivery.DeliveryTime);

                if (deserializeResult.IsFailure)
                {
                    logger.LogWarning(
                        "PolarSharp reconciliation: could not deserialize event for delivery {DeliveryId}. Skipping.",
                        delivery.DeliveryId);
                    skipped++;
                    continue;
                }

                var parsedEvent = deserializeResult.Match(static e => e, static _ => null!);
                await dispatcher.DispatchAsync(parsedEvent, ct).ConfigureAwait(false);
                replayed++;

                if (latestProcessed is null || delivery.DeliveryTime > latestProcessed)
                    latestProcessed = delivery.DeliveryTime;
            }

            if (items.Count < 100)
                break;

            page++;
        }

        logger.LogInformation(
            "PolarSharp reconciliation: replayed {Replayed} events, skipped {Skipped}.",
            replayed, skipped);

        if (latestProcessed.HasValue)
            await checkpoint.SetCheckpointAsync(latestProcessed.Value, ct).ConfigureAwait(false);
    }
}
