using System.Text.Json.Serialization;

namespace PolarSharp.Webhooks.Events;

/// <summary>
/// Fired when a new order is created in Polar.
/// Corresponds to Polar event type <c>order.created</c>.
/// </summary>
public sealed record OrderCreatedEvent : WebhookEvent
{
    /// <summary>Gets the order payload.</summary>
    [JsonPropertyName("data")] public required WebhookOrderData Data { get; init; }
}

/// <summary>
/// Fired when an existing order is updated.
/// Corresponds to Polar event type <c>order.updated</c>.
/// </summary>
public sealed record OrderUpdatedEvent : WebhookEvent
{
    /// <summary>Gets the updated order payload.</summary>
    [JsonPropertyName("data")] public required WebhookOrderData Data { get; init; }
}

/// <summary>
/// Fired when an order's payment is confirmed.
/// Corresponds to Polar event type <c>order.paid</c>.
/// </summary>
public sealed record OrderPaidEvent : WebhookEvent
{
    /// <summary>Gets the paid order payload.</summary>
    [JsonPropertyName("data")] public required WebhookOrderData Data { get; init; }
}

/// <summary>
/// Fired when an order is refunded.
/// Corresponds to Polar event type <c>order.refunded</c>.
/// </summary>
public sealed record OrderRefundedEvent : WebhookEvent
{
    /// <summary>Gets the refunded order payload.</summary>
    [JsonPropertyName("data")] public required WebhookOrderData Data { get; init; }
}
