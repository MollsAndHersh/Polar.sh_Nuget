using System.Text.Json.Serialization;

namespace PolarSharp.Webhooks.Events;

/// <summary>Fired when a new customer is created. Corresponds to <c>customer.created</c>.</summary>
public sealed record CustomerCreatedEvent : WebhookEvent
{
    /// <summary>Gets the customer payload.</summary>
    [JsonPropertyName("data")] public required WebhookCustomerData Data { get; init; }
}

/// <summary>Fired when a customer's profile is updated. Corresponds to <c>customer.updated</c>.</summary>
public sealed record CustomerUpdatedEvent : WebhookEvent
{
    /// <summary>Gets the updated customer payload.</summary>
    [JsonPropertyName("data")] public required WebhookCustomerData Data { get; init; }
}

/// <summary>Fired when a customer's entitlement state changes (subscription activated/deactivated). Corresponds to <c>customer.state_changed</c>.</summary>
public sealed record CustomerStateChangedEvent : WebhookEvent
{
    /// <summary>Gets the customer payload with updated state.</summary>
    [JsonPropertyName("data")] public required WebhookCustomerData Data { get; init; }
}

/// <summary>Fired when a customer is deleted from the organization. Corresponds to <c>customer.deleted</c>.</summary>
public sealed record CustomerDeletedEvent : WebhookEvent
{
    /// <summary>Gets the deleted customer payload.</summary>
    [JsonPropertyName("data")] public required WebhookCustomerData Data { get; init; }
}
