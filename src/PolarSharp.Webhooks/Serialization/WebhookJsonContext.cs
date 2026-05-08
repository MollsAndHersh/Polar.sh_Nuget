using System.Text.Json;
using System.Text.Json.Serialization;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Reconciliation;

namespace PolarSharp.Webhooks.Serialization;

/// <summary>
/// STJ source-generated serialization context for all PolarSharp.Webhooks types.
/// Required for Native AOT and IL trimming compatibility.
/// </summary>
[JsonSerializable(typeof(OrderCreatedEvent))]
[JsonSerializable(typeof(OrderUpdatedEvent))]
[JsonSerializable(typeof(OrderPaidEvent))]
[JsonSerializable(typeof(OrderRefundedEvent))]
[JsonSerializable(typeof(SubscriptionCreatedEvent))]
[JsonSerializable(typeof(SubscriptionActiveEvent))]
[JsonSerializable(typeof(SubscriptionUpdatedEvent))]
[JsonSerializable(typeof(SubscriptionCanceledEvent))]
[JsonSerializable(typeof(SubscriptionUncanceledEvent))]
[JsonSerializable(typeof(SubscriptionPastDueEvent))]
[JsonSerializable(typeof(SubscriptionRevokedEvent))]
[JsonSerializable(typeof(CheckoutCreatedEvent))]
[JsonSerializable(typeof(CheckoutUpdatedEvent))]
[JsonSerializable(typeof(CheckoutExpiredEvent))]
[JsonSerializable(typeof(CustomerCreatedEvent))]
[JsonSerializable(typeof(CustomerUpdatedEvent))]
[JsonSerializable(typeof(CustomerStateChangedEvent))]
[JsonSerializable(typeof(CustomerDeletedEvent))]
[JsonSerializable(typeof(ProductCreatedEvent))]
[JsonSerializable(typeof(ProductUpdatedEvent))]
[JsonSerializable(typeof(BenefitCreatedEvent))]
[JsonSerializable(typeof(BenefitUpdatedEvent))]
[JsonSerializable(typeof(BenefitGrantCreatedEvent))]
[JsonSerializable(typeof(BenefitGrantUpdatedEvent))]
[JsonSerializable(typeof(BenefitGrantCycledEvent))]
[JsonSerializable(typeof(BenefitGrantRevokedEvent))]
[JsonSerializable(typeof(RefundCreatedEvent))]
[JsonSerializable(typeof(RefundUpdatedEvent))]
[JsonSerializable(typeof(UnknownWebhookEvent))]
[JsonSerializable(typeof(WebhookInternalCheckpointData))]
[JsonSourceGenerationOptions(
    MaxDepth = 32,
    AllowTrailingCommas = false,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
internal sealed partial class WebhookJsonContext : JsonSerializerContext
{
}
