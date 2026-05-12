using Microsoft.Extensions.Logging;
using PolarSharp.Webhooks;
using PolarSharp.Webhooks.Events;

namespace PolarTestApp.Handlers;

// ── Base ────────────────────────────────────────────────────────────────────

/// <summary>
/// Generic base handler that logs every incoming webhook event.
/// Used by all 28 concrete handlers registered in PolarTestApp.
/// </summary>
internal abstract class LoggingHandlerBase<TEvent> : PolarWebhookHandlerBase<TEvent>
    where TEvent : WebhookEvent
{
    private readonly ILogger _log;

    protected LoggingHandlerBase(ILogger logger) : base(logger)
    {
        _log = logger;
    }

    protected override Task HandleCoreAsync(TEvent @event, CancellationToken ct)
    {
        _log.LogInformation(
            "Webhook {EventType} received (id={WebhookId})",
            @event.Type, @event.WebhookId);
        return Task.CompletedTask;
    }
}

// ── Orders ──────────────────────────────────────────────────────────────────

internal sealed class OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
    : LoggingHandlerBase<OrderCreatedEvent>(logger);

internal sealed class OrderUpdatedHandler(ILogger<OrderUpdatedHandler> logger)
    : LoggingHandlerBase<OrderUpdatedEvent>(logger);

internal sealed class OrderPaidHandler(ILogger<OrderPaidHandler> logger)
    : LoggingHandlerBase<OrderPaidEvent>(logger);

internal sealed class OrderRefundedHandler(ILogger<OrderRefundedHandler> logger)
    : LoggingHandlerBase<OrderRefundedEvent>(logger);

// ── Subscriptions ───────────────────────────────────────────────────────────

internal sealed class SubscriptionCreatedHandler(ILogger<SubscriptionCreatedHandler> logger)
    : LoggingHandlerBase<SubscriptionCreatedEvent>(logger);

internal sealed class SubscriptionActiveHandler(ILogger<SubscriptionActiveHandler> logger)
    : LoggingHandlerBase<SubscriptionActiveEvent>(logger);

internal sealed class SubscriptionUpdatedHandler(ILogger<SubscriptionUpdatedHandler> logger)
    : LoggingHandlerBase<SubscriptionUpdatedEvent>(logger);

internal sealed class SubscriptionCanceledHandler(ILogger<SubscriptionCanceledHandler> logger)
    : LoggingHandlerBase<SubscriptionCanceledEvent>(logger);

internal sealed class SubscriptionUncanceledHandler(ILogger<SubscriptionUncanceledHandler> logger)
    : LoggingHandlerBase<SubscriptionUncanceledEvent>(logger);

internal sealed class SubscriptionPastDueHandler(ILogger<SubscriptionPastDueHandler> logger)
    : LoggingHandlerBase<SubscriptionPastDueEvent>(logger);

internal sealed class SubscriptionRevokedHandler(ILogger<SubscriptionRevokedHandler> logger)
    : LoggingHandlerBase<SubscriptionRevokedEvent>(logger);

// ── Checkouts ───────────────────────────────────────────────────────────────

internal sealed class CheckoutCreatedHandler(ILogger<CheckoutCreatedHandler> logger)
    : LoggingHandlerBase<CheckoutCreatedEvent>(logger);

internal sealed class CheckoutUpdatedHandler(ILogger<CheckoutUpdatedHandler> logger)
    : LoggingHandlerBase<CheckoutUpdatedEvent>(logger);

internal sealed class CheckoutExpiredHandler(ILogger<CheckoutExpiredHandler> logger)
    : LoggingHandlerBase<CheckoutExpiredEvent>(logger);

// ── Customers ────────────────────────────────────────────────────────────────

internal sealed class CustomerCreatedHandler(ILogger<CustomerCreatedHandler> logger)
    : LoggingHandlerBase<CustomerCreatedEvent>(logger);

internal sealed class CustomerUpdatedHandler(ILogger<CustomerUpdatedHandler> logger)
    : LoggingHandlerBase<CustomerUpdatedEvent>(logger);

internal sealed class CustomerStateChangedHandler(ILogger<CustomerStateChangedHandler> logger)
    : LoggingHandlerBase<CustomerStateChangedEvent>(logger);

internal sealed class CustomerDeletedHandler(ILogger<CustomerDeletedHandler> logger)
    : LoggingHandlerBase<CustomerDeletedEvent>(logger);

// ── Products ─────────────────────────────────────────────────────────────────

internal sealed class ProductCreatedHandler(ILogger<ProductCreatedHandler> logger)
    : LoggingHandlerBase<ProductCreatedEvent>(logger);

internal sealed class ProductUpdatedHandler(ILogger<ProductUpdatedHandler> logger)
    : LoggingHandlerBase<ProductUpdatedEvent>(logger);

// ── Benefits ─────────────────────────────────────────────────────────────────

internal sealed class BenefitCreatedHandler(ILogger<BenefitCreatedHandler> logger)
    : LoggingHandlerBase<BenefitCreatedEvent>(logger);

internal sealed class BenefitUpdatedHandler(ILogger<BenefitUpdatedHandler> logger)
    : LoggingHandlerBase<BenefitUpdatedEvent>(logger);

// ── Benefit Grants ───────────────────────────────────────────────────────────

internal sealed class BenefitGrantCreatedHandler(ILogger<BenefitGrantCreatedHandler> logger)
    : LoggingHandlerBase<BenefitGrantCreatedEvent>(logger);

internal sealed class BenefitGrantUpdatedHandler(ILogger<BenefitGrantUpdatedHandler> logger)
    : LoggingHandlerBase<BenefitGrantUpdatedEvent>(logger);

internal sealed class BenefitGrantCycledHandler(ILogger<BenefitGrantCycledHandler> logger)
    : LoggingHandlerBase<BenefitGrantCycledEvent>(logger);

internal sealed class BenefitGrantRevokedHandler(ILogger<BenefitGrantRevokedHandler> logger)
    : LoggingHandlerBase<BenefitGrantRevokedEvent>(logger);

// ── Refunds ──────────────────────────────────────────────────────────────────

internal sealed class RefundCreatedHandler(ILogger<RefundCreatedHandler> logger)
    : LoggingHandlerBase<RefundCreatedEvent>(logger);

internal sealed class RefundUpdatedHandler(ILogger<RefundUpdatedHandler> logger)
    : LoggingHandlerBase<RefundUpdatedEvent>(logger);
