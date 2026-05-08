using System.Text.Json.Serialization;

namespace PolarSharp.Webhooks.Events;

/// <summary>Fired when a new benefit is created. Corresponds to <c>benefit.created</c>.</summary>
public sealed record BenefitCreatedEvent : WebhookEvent
{
    /// <summary>Gets the benefit payload.</summary>
    [JsonPropertyName("data")] public required WebhookBenefitData Data { get; init; }
}

/// <summary>Fired when an existing benefit is updated. Corresponds to <c>benefit.updated</c>.</summary>
public sealed record BenefitUpdatedEvent : WebhookEvent
{
    /// <summary>Gets the updated benefit payload.</summary>
    [JsonPropertyName("data")] public required WebhookBenefitData Data { get; init; }
}
