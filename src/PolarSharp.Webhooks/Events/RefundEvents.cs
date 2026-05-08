using System.Text.Json.Serialization;

namespace PolarSharp.Webhooks.Events;

/// <summary>Fired when a new refund is initiated. Corresponds to <c>refund.created</c>.</summary>
public sealed record RefundCreatedEvent : WebhookEvent
{
    /// <summary>Gets the refund payload.</summary>
    [JsonPropertyName("data")] public required WebhookRefundData Data { get; init; }
}

/// <summary>Fired when an existing refund is updated (e.g., status changes to succeeded). Corresponds to <c>refund.updated</c>.</summary>
public sealed record RefundUpdatedEvent : WebhookEvent
{
    /// <summary>Gets the updated refund payload.</summary>
    [JsonPropertyName("data")] public required WebhookRefundData Data { get; init; }
}
