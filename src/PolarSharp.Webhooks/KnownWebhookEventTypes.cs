using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks;

/// <summary>
/// The compile-time static list of all known Polar webhook event types.
/// Used by <see cref="PolarWebhookStartupValidator"/> to perform a startup handler completeness check.
/// </summary>
/// <remarks>
/// <para>
/// AOT-safe: no <c>Assembly.GetTypes()</c> reflection. All 28 event types are enumerated
/// explicitly here. A unit test in <c>PolarSharp.Webhooks.Tests</c> verifies this list matches
/// the set of concrete non-abstract types in the <c>Events/</c> folder.
/// </para>
/// <para>
/// Update this list when a new event type is added to the <c>Events/</c> folder.
/// </para>
/// </remarks>
internal static class KnownWebhookEventTypes
{
    /// <summary>Gets all 28 known Polar webhook event types.</summary>
    public static readonly IReadOnlyList<Type> All =
    [
        // Order events (4)
        typeof(OrderCreatedEvent),
        typeof(OrderUpdatedEvent),
        typeof(OrderPaidEvent),
        typeof(OrderRefundedEvent),

        // Subscription events (7)
        typeof(SubscriptionCreatedEvent),
        typeof(SubscriptionActiveEvent),
        typeof(SubscriptionUpdatedEvent),
        typeof(SubscriptionCanceledEvent),
        typeof(SubscriptionUncanceledEvent),
        typeof(SubscriptionPastDueEvent),
        typeof(SubscriptionRevokedEvent),

        // Checkout events (3)
        typeof(CheckoutCreatedEvent),
        typeof(CheckoutUpdatedEvent),
        typeof(CheckoutExpiredEvent),

        // Customer events (4)
        typeof(CustomerCreatedEvent),
        typeof(CustomerUpdatedEvent),
        typeof(CustomerStateChangedEvent),
        typeof(CustomerDeletedEvent),

        // Product events (2)
        typeof(ProductCreatedEvent),
        typeof(ProductUpdatedEvent),

        // Benefit events (2)
        typeof(BenefitCreatedEvent),
        typeof(BenefitUpdatedEvent),

        // Benefit grant events (4)
        typeof(BenefitGrantCreatedEvent),
        typeof(BenefitGrantUpdatedEvent),
        typeof(BenefitGrantCycledEvent),
        typeof(BenefitGrantRevokedEvent),

        // Refund events (2)
        typeof(RefundCreatedEvent),
        typeof(RefundUpdatedEvent),
    ];
}
