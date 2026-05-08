using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Serialization.Json;

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

        var startTimestamp = lowerBound.ToUnixTimeSeconds().ToString();

        logger.LogInformation(
            "PolarSharp reconciliation: checking failed deliveries since {Since}.",
            lowerBound);

        await using var scope = scopeFactory.CreateAsyncScope();
        var polar = scope.ServiceProvider.GetRequiredService<PolarClient>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IPolarWebhookDispatcher>();

        int page = 1;
        int replayed = 0;
        int skipped = 0;
        DateTimeOffset? latestProcessed = null;

        while (true)
        {
            var response = await polar.Webhooks.Deliveries.GetAsync(config =>
            {
                config.QueryParameters.StartTimestamp = startTimestamp;
                config.QueryParameters.Succeeded = false;
                config.QueryParameters.Limit = 100;
                config.QueryParameters.Page = page;
            }, ct).ConfigureAwait(false);

            var items = response?.Items;
            if (items is null || items.Count == 0)
                break;

            foreach (var delivery in items)
            {
                var webhookEvent = delivery.WebhookEvent;
                if (webhookEvent is null || webhookEvent.IsArchived == true)
                {
                    skipped++;
                    continue;
                }

                var deliveryId = webhookEvent.Id ?? Guid.NewGuid().ToString();
                var deliveryTime = delivery.CreatedAt ?? DateTimeOffset.UtcNow;
                var payloadJson = SerializePayload(webhookEvent.Payload);

                if (string.IsNullOrEmpty(payloadJson))
                {
                    logger.LogWarning(
                        "PolarSharp reconciliation: delivery {DeliveryId} has no serializable payload. Skipping.",
                        deliveryId);
                    skipped++;
                    continue;
                }

                var deserializeResult = validator.Deserialize(payloadJson, deliveryId, deliveryTime);

                if (deserializeResult.IsFailure)
                {
                    logger.LogWarning(
                        "PolarSharp reconciliation: could not deserialize event for delivery {DeliveryId}. Skipping.",
                        deliveryId);
                    skipped++;
                    continue;
                }

                var parsedEvent = deserializeResult.Match(static e => e, static _ => null!);
                await dispatcher.DispatchAsync(parsedEvent, ct).ConfigureAwait(false);
                replayed++;

                if (latestProcessed is null || deliveryTime > latestProcessed)
                    latestProcessed = deliveryTime;
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

    private static string? SerializePayload(
        global::PolarSharp.Generated.Models.WebhookEvent.WebhookEvent_payload? payload)
    {
        if (payload is null)
            return null;

        // Fast path: Polar may return the payload as a JSON string literal.
        // WebhookEvent_payload is a Kiota union type (string | object); when the raw
        // JSON value is a string, Kiota populates String directly — no serialization needed.
        if (payload.String is { Length: > 0 } json)
            return json;

        // Object path: use JsonSerializationWriter directly — AOT-safe, reflection-free.
        // Calls payload.Serialize(writer) which is hand-written Kiota-generated code,
        // not bare JsonSerializer.Serialize which requires a registered JsonTypeInfo.
        try
        {
            using var writer = new JsonSerializationWriter();
            payload.Serialize(writer);
            using var stream = writer.GetSerializedContent();
            using var reader = new System.IO.StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
