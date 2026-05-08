using System.Text.Json.Serialization;

namespace PolarSharp.Webhooks.Events;

/// <summary>Fired when a new subscription is created. Corresponds to <c>subscription.created</c>.</summary>
public sealed record SubscriptionCreatedEvent : WebhookEvent
{
    /// <summary>Gets the subscription payload.</summary>
    [JsonPropertyName("data")] public required WebhookSubscriptionData Data { get; init; }
}

/// <summary>Fired when a subscription becomes active (first payment confirmed). Corresponds to <c>subscription.active</c>.</summary>
public sealed record SubscriptionActiveEvent : WebhookEvent
{
    /// <summary>Gets the subscription payload.</summary>
    [JsonPropertyName("data")] public required WebhookSubscriptionData Data { get; init; }
}

/// <summary>Fired when a subscription is updated (e.g., plan change). Corresponds to <c>subscription.updated</c>.</summary>
public sealed record SubscriptionUpdatedEvent : WebhookEvent
{
    /// <summary>Gets the updated subscription payload.</summary>
    [JsonPropertyName("data")] public required WebhookSubscriptionData Data { get; init; }
}

/// <summary>Fired when a customer cancels a subscription. Corresponds to <c>subscription.canceled</c>.</summary>
public sealed record SubscriptionCanceledEvent : WebhookEvent
{
    /// <summary>Gets the canceled subscription payload.</summary>
    [JsonPropertyName("data")] public required WebhookSubscriptionData Data { get; init; }
}

/// <summary>Fired when a canceled subscription is un-canceled before the period ends. Corresponds to <c>subscription.uncanceled</c>.</summary>
public sealed record SubscriptionUncanceledEvent : WebhookEvent
{
    /// <summary>Gets the subscription payload.</summary>
    [JsonPropertyName("data")] public required WebhookSubscriptionData Data { get; init; }
}

/// <summary>Fired when a subscription enters a past-due state (payment failed). Corresponds to <c>subscription.past_due</c>.</summary>
public sealed record SubscriptionPastDueEvent : WebhookEvent
{
    /// <summary>Gets the past-due subscription payload.</summary>
    [JsonPropertyName("data")] public required WebhookSubscriptionData Data { get; init; }
}

/// <summary>Fired when a subscription is permanently revoked (unpaid past-due expiry). Corresponds to <c>subscription.revoked</c>.</summary>
public sealed record SubscriptionRevokedEvent : WebhookEvent
{
    /// <summary>Gets the revoked subscription payload.</summary>
    [JsonPropertyName("data")] public required WebhookSubscriptionData Data { get; init; }
}
