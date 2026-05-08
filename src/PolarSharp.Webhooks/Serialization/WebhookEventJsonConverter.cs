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

        // Use the (string, JsonTypeInfo<T>) overload for each branch — AOT-safe, no reflection.
        return eventType switch
        {
            "order.created"              => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.OrderCreatedEvent),
            "order.updated"              => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.OrderUpdatedEvent),
            "order.paid"                 => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.OrderPaidEvent),
            "order.refunded"             => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.OrderRefundedEvent),
            "subscription.created"       => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.SubscriptionCreatedEvent),
            "subscription.active"        => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.SubscriptionActiveEvent),
            "subscription.updated"       => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.SubscriptionUpdatedEvent),
            "subscription.canceled"      => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.SubscriptionCanceledEvent),
            "subscription.uncanceled"    => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.SubscriptionUncanceledEvent),
            "subscription.past_due"      => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.SubscriptionPastDueEvent),
            "subscription.revoked"       => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.SubscriptionRevokedEvent),
            "checkout.created"           => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.CheckoutCreatedEvent),
            "checkout.updated"           => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.CheckoutUpdatedEvent),
            "checkout.expired"           => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.CheckoutExpiredEvent),
            "customer.created"           => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.CustomerCreatedEvent),
            "customer.updated"           => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.CustomerUpdatedEvent),
            "customer.state_changed"     => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.CustomerStateChangedEvent),
            "customer.deleted"           => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.CustomerDeletedEvent),
            "product.created"            => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.ProductCreatedEvent),
            "product.updated"            => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.ProductUpdatedEvent),
            "benefit.created"            => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.BenefitCreatedEvent),
            "benefit.updated"            => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.BenefitUpdatedEvent),
            "benefit_grant.created"      => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.BenefitGrantCreatedEvent),
            "benefit_grant.updated"      => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.BenefitGrantUpdatedEvent),
            "benefit_grant.cycled"       => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.BenefitGrantCycledEvent),
            "benefit_grant.revoked"      => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.BenefitGrantRevokedEvent),
            "refund.created"             => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.RefundCreatedEvent),
            "refund.updated"             => JsonSerializer.Deserialize(json, WebhookJsonContext.Default.RefundUpdatedEvent),
            _                            => new UnknownWebhookEvent { Type = eventType, WebhookId = "", Timestamp = DateTimeOffset.UtcNow },
        };
    }

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
