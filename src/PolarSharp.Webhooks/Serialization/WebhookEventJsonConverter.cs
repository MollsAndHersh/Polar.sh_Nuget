using System.Text.Json;
using System.Text.Json.Serialization;
using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks.Serialization;

/// <summary>
/// Source-generated STJ converter that resolves the concrete <see cref="WebhookEvent"/> type
/// from the <c>type</c> discriminator property in the Polar webhook JSON payload.
/// </summary>
/// <remarks>
/// AOT-safe: uses a static, compile-time dictionary — no reflection, no expression compilation.
/// Unknown event types deserialize as <see cref="UnknownWebhookEvent"/> so the dispatcher can
/// log them without failing the request.
/// </remarks>
internal sealed class WebhookEventJsonConverter : JsonConverter<WebhookEvent>
{
    /// <inheritdoc/>
    public override WebhookEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var eventType = root.TryGetProperty("type", out var typeProp)
            ? typeProp.GetString() ?? ""
            : "";

        var json = root.GetRawText();
        return eventType switch
        {
            "order.created"              => Deserialize<OrderCreatedEvent>(json),
            "order.updated"              => Deserialize<OrderUpdatedEvent>(json),
            "order.paid"                 => Deserialize<OrderPaidEvent>(json),
            "order.refunded"             => Deserialize<OrderRefundedEvent>(json),
            "subscription.created"       => Deserialize<SubscriptionCreatedEvent>(json),
            "subscription.active"        => Deserialize<SubscriptionActiveEvent>(json),
            "subscription.updated"       => Deserialize<SubscriptionUpdatedEvent>(json),
            "subscription.canceled"      => Deserialize<SubscriptionCanceledEvent>(json),
            "subscription.uncanceled"    => Deserialize<SubscriptionUncanceledEvent>(json),
            "subscription.past_due"      => Deserialize<SubscriptionPastDueEvent>(json),
            "subscription.revoked"       => Deserialize<SubscriptionRevokedEvent>(json),
            "checkout.created"           => Deserialize<CheckoutCreatedEvent>(json),
            "checkout.updated"           => Deserialize<CheckoutUpdatedEvent>(json),
            "checkout.expired"           => Deserialize<CheckoutExpiredEvent>(json),
            "customer.created"           => Deserialize<CustomerCreatedEvent>(json),
            "customer.updated"           => Deserialize<CustomerUpdatedEvent>(json),
            "customer.state_changed"     => Deserialize<CustomerStateChangedEvent>(json),
            "customer.deleted"           => Deserialize<CustomerDeletedEvent>(json),
            "product.created"            => Deserialize<ProductCreatedEvent>(json),
            "product.updated"            => Deserialize<ProductUpdatedEvent>(json),
            "benefit.created"            => Deserialize<BenefitCreatedEvent>(json),
            "benefit.updated"            => Deserialize<BenefitUpdatedEvent>(json),
            "benefit_grant.created"      => Deserialize<BenefitGrantCreatedEvent>(json),
            "benefit_grant.updated"      => Deserialize<BenefitGrantUpdatedEvent>(json),
            "benefit_grant.cycled"       => Deserialize<BenefitGrantCycledEvent>(json),
            "benefit_grant.revoked"      => Deserialize<BenefitGrantRevokedEvent>(json),
            "refund.created"             => Deserialize<RefundCreatedEvent>(json),
            "refund.updated"             => Deserialize<RefundUpdatedEvent>(json),
            _                            => new UnknownWebhookEvent { Type = eventType, WebhookId = "", Timestamp = DateTimeOffset.UtcNow },
        };
    }

    private static T Deserialize<T>(string json) where T : WebhookEvent
        => JsonSerializer.Deserialize<T>(json, WebhookJsonContext.Default.Options)!;

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, WebhookEvent value, JsonSerializerOptions options)
        => throw new NotSupportedException("Polar webhook events are read-only — serialization is not supported.");
}

/// <summary>
/// Placeholder for Polar webhook event types not recognized by this SDK version.
/// Returned by <see cref="WebhookEventJsonConverter"/> when the <c>type</c> discriminator
/// is not in the known event type registry.
/// </summary>
/// <remarks>
/// Allows the dispatcher to acknowledge and log unknown event types rather than failing
/// the HTTP 200 response, which would cause Polar to retry indefinitely.
/// </remarks>
public sealed record UnknownWebhookEvent : WebhookEvent
{
    /// <summary>Gets the raw JSON payload of the unrecognized event.</summary>
    /// <value>The full JSON string from Polar's webhook body. Useful for debugging forward-compatibility gaps.</value>
    public string RawJson { get; init; } = "";
}
