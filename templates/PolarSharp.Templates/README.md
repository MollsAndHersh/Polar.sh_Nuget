# PolarSharp.Templates

`dotnet new` templates for **PolarSharp** — scaffold Polar.sh webhook handlers in one command.

## Install

```bash
dotnet new install PolarSharp.Templates
```

## Usage

```bash
dotnet new polar-handler --event OrderCreatedEvent --name OrderCreatedHandler --output Handlers/
```

This generates `Handlers/OrderCreatedHandler.cs` — a compilable, XML-documented handler with the correct `PolarWebhookHandlerBase<OrderCreatedEvent>` base class and every available `@event.Data` property listed in the XML docs.

## Supported event types

Pass any of the following to `--event`:

| Event type | Polar event string |
|---|---|
| `OrderCreatedEvent` | `order.created` |
| `OrderUpdatedEvent` | `order.updated` |
| `OrderPaidEvent` | `order.paid` |
| `OrderRefundedEvent` | `order.refunded` |
| `SubscriptionCreatedEvent` | `subscription.created` |
| `SubscriptionActiveEvent` | `subscription.active` |
| `SubscriptionUpdatedEvent` | `subscription.updated` |
| `SubscriptionCanceledEvent` | `subscription.canceled` |
| `SubscriptionUncanceledEvent` | `subscription.uncanceled` |
| `SubscriptionPastDueEvent` | `subscription.past_due` |
| `SubscriptionRevokedEvent` | `subscription.revoked` |
| `CheckoutCreatedEvent` | `checkout.created` |
| `CheckoutUpdatedEvent` | `checkout.updated` |
| `CheckoutExpiredEvent` | `checkout.expired` |
| `CustomerCreatedEvent` | `customer.created` |
| `CustomerUpdatedEvent` | `customer.updated` |
| `CustomerStateChangedEvent` | `customer.state_changed` |
| `CustomerDeletedEvent` | `customer.deleted` |
| `ProductCreatedEvent` | `product.created` |
| `ProductUpdatedEvent` | `product.updated` |
| `BenefitCreatedEvent` | `benefit.created` |
| `BenefitUpdatedEvent` | `benefit.updated` |
| `BenefitGrantCreatedEvent` | `benefit_grant.created` |
| `BenefitGrantUpdatedEvent` | `benefit_grant.updated` |
| `BenefitGrantCycledEvent` | `benefit_grant.cycled` |
| `BenefitGrantRevokedEvent` | `benefit_grant.revoked` |
| `RefundCreatedEvent` | `refund.created` |
| `RefundUpdatedEvent` | `refund.updated` |

## After scaffolding

Register the handler in `Program.cs`:

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddWebhookHandler<OrderCreatedEvent, OrderCreatedHandler>();
```
