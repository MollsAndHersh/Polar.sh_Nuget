# PolarSharp.Webhooks

Webhook handling for the [PolarSharp](https://www.nuget.org/packages/PolarSharp) .NET SDK. Provides HMAC-SHA256 signature verification, strongly-typed event dispatch, real-time toast notifications, and webhook reconciliation.

## Installation

```bash
dotnet add package PolarSharp
dotnet add package PolarSharp.Webhooks
```

## Quick Start

```csharp
// Program.cs
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddWebhookHandler<OrderCreatedEvent, OrderCreatedHandler>();

app.UsePolarInfrastructure();   // maps POST /hooks/polar

// appsettings.json
{
  "PolarSharp": {
    "Webhooks": {
      "Secret": "whsec_xxx",
      "Path":   "/hooks/polar"
    }
  }
}
```

```csharp
// Handlers/OrderCreatedHandler.cs
public sealed class OrderCreatedHandler : PolarWebhookHandlerBase<OrderCreatedEvent>
{
    private readonly IOrderService _orders;

    public OrderCreatedHandler(IOrderService orders, ILogger<OrderCreatedHandler> logger)
        : base(logger) => _orders = orders;

    protected override Task HandleCoreAsync(OrderCreatedEvent @event, CancellationToken ct)
        => _orders.FulfillAsync(OrderId.From(@event.Data.Id), ct);
}
```

Scaffold a handler instantly:
```bash
dotnet new install PolarSharp.Templates
dotnet new polar-handler --event OrderCreatedEvent --name OrderCreatedHandler
```

## Features

- **HMAC-SHA256 verification** — Standard Webhooks spec; timing-uniform responses
- **Multi-secret rotation** — both old and new secrets active during zero-downtime rotation
- **28 typed event records** — `OrderCreatedEvent`, `SubscriptionActiveEvent`, etc.
- **Handler completeness check** — startup warning (or hard fail) when event types have no handler
- **Background queue** — `enqueue: true` returns 200 immediately; slow handlers run off the request path
- **Toast notifications** — `IPolarToastChannel` feeds Blazor/SignalR/SSE real-time admin UIs
- **Webhook reconciliation** — periodic replay of missed events via `polar.Events.ListAsync`
- **Security hardening** — payload size cap, rate limiting, IP allowlist, content-type enforcement
- **MVC filter** — `[ValidatePolarWebhook]` for controller-based handlers

## Documentation

- [Webhook Setup](https://markchipman.github.io/PolarSharp/articles/webhooks.html)
- [Handler Patterns](https://markchipman.github.io/PolarSharp/articles/webhook-handlers.html)
- [Toast Notifications](https://markchipman.github.io/PolarSharp/articles/toast-notifications.html)
- [Security Hardening](https://markchipman.github.io/PolarSharp/articles/security.html)

## License

MIT
