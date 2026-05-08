# Webhook Handlers

## Handler registration

Register one handler per event type using the fluent builder:

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddWebhookHandler<OrderCreatedEvent,         OrderCreatedHandler>()
    .AddWebhookHandler<OrderPaidEvent,            OrderPaidHandler>()
    .AddWebhookHandler<SubscriptionActiveEvent,   SubscriptionActiveHandler>()
    .AddWebhookHandler<SubscriptionCanceledEvent, SubscriptionCanceledHandler>()
    .AddWebhookHandler<CustomerCreatedEvent,      CustomerCreatedHandler>()
    .AddWebhookHandler<RefundCreatedEvent,        RefundHandler>();
```

Handlers are registered as `Scoped` DI services — they participate in the incoming HTTP request's DI scope, so you can inject `DbContext`, `ICurrentUser`, and other scoped services normally.

## All known event types

| Event type string | .NET record type |
|---|---|
| `order.created` | `OrderCreatedEvent` |
| `order.updated` | `OrderUpdatedEvent` |
| `order.paid` | `OrderPaidEvent` |
| `order.fulfilled` | `OrderFulfilledEvent` |
| `subscription.created` | `SubscriptionCreatedEvent` |
| `subscription.active` | `SubscriptionActiveEvent` |
| `subscription.canceled` | `SubscriptionCanceledEvent` |
| `subscription.revoked` | `SubscriptionRevokedEvent` |
| `checkout.created` | `CheckoutCreatedEvent` |
| `checkout.updated` | `CheckoutUpdatedEvent` |
| `customer.created` | `CustomerCreatedEvent` |
| `customer.updated` | `CustomerUpdatedEvent` |
| `product.created` | `ProductCreatedEvent` |
| `product.updated` | `ProductUpdatedEvent` |
| `benefit.created` | `BenefitCreatedEvent` |
| `benefit.grant.created` | `BenefitGrantCreatedEvent` |
| `benefit.grant.updated` | `BenefitGrantUpdatedEvent` |
| `benefit.grant.revoked` | `BenefitGrantRevokedEvent` |
| `refund.created` | `RefundCreatedEvent` |
| `refund.updated` | `RefundUpdatedEvent` |

## Handler base class

`PolarWebhookHandlerBase<TEvent>` seals all infrastructure orchestration. Implement only `HandleCoreAsync`:

```csharp
public sealed class OrderPaidHandler : PolarWebhookHandlerBase<OrderPaidEvent>
{
    private readonly IEntitlementService _entitlements;
    private readonly IEmailSender _email;

    public OrderPaidHandler(
        IEntitlementService entitlements,
        IEmailSender email,
        ILogger<OrderPaidHandler> logger) : base(logger)
    {
        _entitlements = entitlements;
        _email = email;
    }

    protected override async Task HandleCoreAsync(OrderPaidEvent @event, CancellationToken ct)
    {
        // Use @event.WebhookId for idempotency — Polar delivers at-least-once
        var orderId = OrderId.From(@event.Data.Id);
        await _entitlements.GrantAsync(orderId, ct);
        await _email.SendReceiptAsync(@event.Data.Customer.Email, @event.Data, ct);
    }
}
```

The base class handles:
- Logging `Information` on event received / handled
- Cancellation token scoping (HTTP disconnect does NOT abort an in-flight handler)
- Metrics via `polar.webhooks.received`

## Completeness check

`PolarWebhookStartupValidator` runs before the app accepts traffic and warns about every unhandled event type:

```
[WRN] PolarSharp Webhooks: No handler registered for event type 'product.updated'
      (ProductUpdatedEvent). If Polar delivers this event, it will be silently discarded.
      Register: .AddWebhookHandler<ProductUpdatedEvent, YourHandler>()
```

Set `FailOnMissingHandlers: true` in `appsettings.json` to fail startup instead:

```json
{
  "PolarSharp": {
    "Webhooks": {
      "FailOnMissingHandlers": true
    }
  }
}
```

## `dotnet new polar-handler` scaffold

Install the template pack once and scaffold any handler in one command:

```bash
dotnet new install PolarSharp.Templates
dotnet new polar-handler --event OrderCreatedEvent --name OrderCreatedHandler --output Handlers/
```

The generated file is compilable, XML-documented, and includes all available `@event.Data` property references for the chosen event type.
