using System.Text.Json.Serialization;

namespace PolarSharp.Webhooks.Events;

/// <summary>Fired when a new product is created. Corresponds to <c>product.created</c>.</summary>
public sealed record ProductCreatedEvent : WebhookEvent
{
    /// <summary>Gets the product payload.</summary>
    [JsonPropertyName("data")] public required WebhookProductData Data { get; init; }
}

/// <summary>Fired when an existing product is updated. Corresponds to <c>product.updated</c>.</summary>
public sealed record ProductUpdatedEvent : WebhookEvent
{
    /// <summary>Gets the updated product payload.</summary>
    [JsonPropertyName("data")] public required WebhookProductData Data { get; init; }
}
