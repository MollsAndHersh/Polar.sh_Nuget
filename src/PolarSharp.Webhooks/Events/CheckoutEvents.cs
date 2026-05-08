using System.Text.Json.Serialization;

namespace PolarSharp.Webhooks.Events;

/// <summary>Fired when a new checkout session is created. Corresponds to <c>checkout.created</c>.</summary>
public sealed record CheckoutCreatedEvent : WebhookEvent
{
    /// <summary>Gets the checkout payload.</summary>
    [JsonPropertyName("data")] public required WebhookCheckoutData Data { get; init; }
}

/// <summary>Fired when a checkout session is updated (e.g., customer fills in email). Corresponds to <c>checkout.updated</c>.</summary>
public sealed record CheckoutUpdatedEvent : WebhookEvent
{
    /// <summary>Gets the updated checkout payload.</summary>
    [JsonPropertyName("data")] public required WebhookCheckoutData Data { get; init; }
}

/// <summary>Fired when a checkout session expires without completing. Corresponds to <c>checkout.expired</c>.</summary>
public sealed record CheckoutExpiredEvent : WebhookEvent
{
    /// <summary>Gets the expired checkout payload.</summary>
    [JsonPropertyName("data")] public required WebhookCheckoutData Data { get; init; }
}
