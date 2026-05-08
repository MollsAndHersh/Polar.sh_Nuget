using System.Text.Json.Serialization;

namespace PolarSharp.Webhooks.Events;

/// <summary>Fired when a benefit is granted to a customer. Corresponds to <c>benefit_grant.created</c>.</summary>
public sealed record BenefitGrantCreatedEvent : WebhookEvent
{
    /// <summary>Gets the benefit grant payload.</summary>
    [JsonPropertyName("data")] public required WebhookBenefitGrantData Data { get; init; }
}

/// <summary>Fired when an existing benefit grant is updated. Corresponds to <c>benefit_grant.updated</c>.</summary>
public sealed record BenefitGrantUpdatedEvent : WebhookEvent
{
    /// <summary>Gets the updated benefit grant payload.</summary>
    [JsonPropertyName("data")] public required WebhookBenefitGrantData Data { get; init; }
}

/// <summary>
/// Fired when a benefit grant is cycled (renewed for the next billing period).
/// Corresponds to <c>benefit_grant.cycled</c>.
/// </summary>
public sealed record BenefitGrantCycledEvent : WebhookEvent
{
    /// <summary>Gets the cycled benefit grant payload.</summary>
    [JsonPropertyName("data")] public required WebhookBenefitGrantData Data { get; init; }
}

/// <summary>Fired when a benefit grant is revoked from a customer. Corresponds to <c>benefit_grant.revoked</c>.</summary>
public sealed record BenefitGrantRevokedEvent : WebhookEvent
{
    /// <summary>Gets the revoked benefit grant payload.</summary>
    [JsonPropertyName("data")] public required WebhookBenefitGrantData Data { get; init; }
}
