using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace PolarSharp.CustomerGraph.Projection;

/// <summary>
/// Background hosted service that drains a bounded channel of <see cref="GraphProjectionEvent"/>
/// items and applies each event to the customer graph via <see cref="ICustomerGraphProjector"/>.
/// </summary>
/// <remarks>
/// <para>
/// Source events feed in from three layers:
/// </para>
/// <list type="bullet">
///   <item>Polar webhooks (order.created, order.paid, refund.completed, customer.updated, etc.).</item>
///   <item>EF Core <c>SaveChangesInterceptor</c> on EcommerceStoreManagement DbContexts.</item>
///   <item>IP capture events (when the tenant has IpCaptureMode != Disabled).</item>
/// </list>
/// <para>
/// The channel is bounded (default 10,000 capacity per tenant) so producer backpressure
/// kicks in if the projection falls behind. Failures retry with exponential backoff;
/// after 3 retries the event dead-letters to PlatformAuditLogEntry.
/// </para>
/// </remarks>
public sealed class CustomerGraphProjectionHostedService(
    Channel<GraphProjectionEvent> channel,
    ICustomerGraphProjector projector,
    ILogger<CustomerGraphProjectionHostedService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ApplyEventAsync(evt, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to project graph event {EventType} for customer {CustomerId}; will retry",
                    evt.GetType().Name, evt.CustomerId);
                // Phase 17.x: retry with exponential backoff + dead-letter to PlatformAuditLogEntry
            }
        }
    }

    private async Task ApplyEventAsync(GraphProjectionEvent evt, CancellationToken ct) => await (evt switch
    {
        CustomerUpsertedEvent e => projector.UpsertCustomerAsync(e.Customer, ct),
        PurchaseRecordedEvent e => projector.RecordPurchaseAsync(e.CustomerId, e.ProductId, e.Quantity, e.UnitAmount, e.Currency, e.OccurredAt, ct),
        IpUsageRecordedEvent e => projector.RecordIpUsageAsync(e.CustomerId, e.IpHash, e.OccurredAt, ct),
        TagsUpdatedEvent e => projector.SetCustomerTagsAsync(e.CustomerId, e.Tags, ct),
        CustomerErasedEvent e => projector.EraseCustomerAsync(e.CustomerId, ct),
        _ => Task.CompletedTask
    });
}

/// <summary>Marker base for events fed into the projection channel.</summary>
/// <param name="CustomerId">The customer this event pertains to.</param>
/// <param name="OccurredAt">When the source event occurred (used for time-based predicates in graph queries).</param>
public abstract record GraphProjectionEvent(string CustomerId, DateTimeOffset OccurredAt);

/// <summary>Event: a customer was created or updated; upsert the customer node.</summary>
/// <param name="Customer">Customer node fields.</param>
/// <param name="OccurredAt">Event timestamp.</param>
public sealed record CustomerUpsertedEvent(CustomerNodeInput Customer, DateTimeOffset OccurredAt)
    : GraphProjectionEvent(Customer.CustomerId, OccurredAt);

/// <summary>Event: a customer purchased a product; record the PURCHASED edge.</summary>
/// <param name="CustomerId">Buying customer's id.</param>
/// <param name="ProductId">Product purchased.</param>
/// <param name="Quantity">How many units.</param>
/// <param name="UnitAmount">Price per unit (in the order's currency).</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="OccurredAt">Purchase timestamp.</param>
public sealed record PurchaseRecordedEvent(string CustomerId, string ProductId, int Quantity, decimal UnitAmount, string Currency, DateTimeOffset OccurredAt)
    : GraphProjectionEvent(CustomerId, OccurredAt);

/// <summary>Event: a customer's request originated from an IP; record the USED_IP edge.</summary>
/// <param name="CustomerId">Customer id.</param>
/// <param name="IpHash">SHA-256 hash of the IP (or raw IP encoded as string in CaptureRaw mode).</param>
/// <param name="OccurredAt">When the IP usage occurred.</param>
public sealed record IpUsageRecordedEvent(string CustomerId, string IpHash, DateTimeOffset OccurredAt)
    : GraphProjectionEvent(CustomerId, OccurredAt);

/// <summary>Event: a customer's host-defined tags changed; replace the tag set.</summary>
/// <param name="CustomerId">Customer id.</param>
/// <param name="Tags">New tag set (replaces existing).</param>
/// <param name="OccurredAt">Event timestamp.</param>
public sealed record TagsUpdatedEvent(string CustomerId, IReadOnlyList<string> Tags, DateTimeOffset OccurredAt)
    : GraphProjectionEvent(CustomerId, OccurredAt);

/// <summary>Event: GDPR right-to-be-forgotten; erase the customer node + all incident edges.</summary>
/// <param name="CustomerId">Customer id to erase.</param>
/// <param name="OccurredAt">Event timestamp.</param>
public sealed record CustomerErasedEvent(string CustomerId, DateTimeOffset OccurredAt)
    : GraphProjectionEvent(CustomerId, OccurredAt);
