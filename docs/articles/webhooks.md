# Webhooks

PolarSharp verifies Polar webhook signatures per the [Standard Webhooks](https://www.standardwebhooks.com/) specification and dispatches events to typed handler classes you register in DI.

## Setup

### 1. Configure the webhook secret

```json
{
  "PolarSharp": {
    "Webhooks": {
      "Secret": "whsec_xxx",
      "Path": "/hooks/polar"
    }
  }
}
```

Get your webhook secret from the Polar dashboard after creating a webhook endpoint pointed at `https://yourdomain.com/hooks/polar`.

### 2. Register in `Program.cs`

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddWebhookHandler<OrderCreatedEvent, OrderCreatedHandler>()
    .AddWebhookHandler<SubscriptionActiveEvent, SubscriptionActiveHandler>();

// ...

app.UsePolarInfrastructure();   // maps POST /hooks/polar automatically
```

## Implementing a handler

Inherit from `PolarWebhookHandlerBase<TEvent>` and implement `HandleCoreAsync`:

```csharp
public sealed class OrderCreatedHandler : PolarWebhookHandlerBase<OrderCreatedEvent>
{
    private readonly IOrderService _orders;

    public OrderCreatedHandler(IOrderService orders, ILogger<OrderCreatedHandler> logger)
        : base(logger)
        => _orders = orders;

    protected override async Task HandleCoreAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Implement idempotent fulfillment logic using @event.WebhookId for deduplication
        await _orders.FulfillAsync(OrderId.From(@event.Data.Id), ct);
    }
}
```

**Idempotency is required.** Polar delivers webhooks at-least-once. Use `@event.WebhookId` as a deduplication key (DB upsert, distributed lock, or outbox pattern).

## Signature verification

PolarSharp verifies every incoming webhook:

1. Reads `webhook-id`, `webhook-timestamp`, `webhook-signature` headers
2. Validates timestamp within ±5 minutes (configurable `ToleranceSeconds`)
3. Computes `HMAC-SHA256(secret, "{webhook-id}.{webhook-timestamp}.{body}")`
4. Compares against all entries in `webhook-signature` (comma-separated for multi-secret rotation) using constant-time comparison

Verification failures return HTTP 400 with an opaque identical error body — no timing oracle leakage.

## Zero-downtime secret rotation

Use a list of secrets during the rotation window:

```json
{
  "PolarSharp": {
    "Webhooks": {
      "Secrets": ["whsec_new_xxx", "whsec_old_xxx"]
    }
  }
}
```

Verification passes if either secret produces a matching signature. Once Polar's dashboard is updated to the new secret, remove the old entry from your config.

## Startup completeness check

At startup, PolarSharp warns about any known Polar event types that have no registered handler:

```
[WRN] PolarSharp Webhooks: 2 of 28 known Polar event types have no registered handler:
      - subscription.canceled  → register AddWebhookHandler<SubscriptionCanceledEvent, THandler>()
      - refund.updated         → register AddWebhookHandler<RefundUpdatedEvent, THandler>()
```

Set `PolarSharp:Webhooks:FailOnMissingHandlers: true` to fail startup instead of warning.

## Slow handlers — background queue

If your handler takes more than ~5 seconds, wrap it in the background queue to return 200 to Polar immediately:

```csharp
.AddWebhookHandler<OrderCreatedEvent, OrderCreatedHandler>(enqueue: true)
```

The event is queued in a bounded `Channel<T>` and processed by a background `IHostedService`. The channel drains gracefully on shutdown (default: 30s timeout).

## Testing with the simulator

`PolarTestApp` includes `POST /test/webhook/simulate/{eventType}` — constructs a real HMAC-signed payload and posts it to the app's own webhook endpoint, proving the full verification pipeline without needing an external sender.
