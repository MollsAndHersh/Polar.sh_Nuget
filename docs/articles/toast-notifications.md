# Toast Notifications

PolarSharp can deliver real-time UI toast notifications when configured Polar webhook events arrive — without polling. The host app reads from a bounded `Channel<PolarToastNotification>` and wires it to any UI notification system.

## Architecture

```
Polar webhook → WebhookValidator → IPolarWebhookDispatcher
                                          │
                                          ├─► IPolarWebhookHandler<TEvent>  (your business logic)
                                          │
                                          └─► IPolarToastChannel.Writer.TryWrite(...)
                                                      │
                                                    Channel<PolarToastNotification> (bounded)
                                                      │
                                              ┌───────┴────────────┐
                                          Blazor              SignalR / SSE
                                          circuit            hub broadcast
```

The channel is bounded (default 100 slots, configurable). When full, `TryWrite` drops the oldest entry silently — the business event was already handled by your webhook handler, so no data is lost.

## Registration

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddPolarToastNotifications();   // reads PolarSharp:Webhooks:ToastNotifications
```

Or with inline configuration:

```csharp
    .AddPolarToastNotifications(opts =>
    {
        opts.ChannelCapacity = 200;
        opts.Events.Add(new PolarToastEventConfig
        {
            EventType       = "order.created",
            Title           = "New Order",
            MessageTemplate = "Order #{OrderNumber} from {CustomerEmail}",
            Severity        = ToastSeverity.Success,
            DurationSeconds = 5,
        });
    });
```

## `appsettings.json` whitelist

Only event types listed in `Events` generate toast notifications. Unlisted event types are silently ignored.

```json
{
  "PolarSharp": {
    "Webhooks": {
      "ToastNotifications": {
        "Enabled": true,
        "ChannelCapacity": 100,
        "Events": [
          {
            "EventType":       "order.created",
            "Title":           "New Order",
            "MessageTemplate": "Order #{OrderNumber} by {CustomerEmail} — {TotalAmount} {Currency}",
            "Severity":        "Success",
            "DurationSeconds": 5
          },
          {
            "EventType":       "subscription.canceled",
            "Title":           "Subscription Canceled",
            "MessageTemplate": "{CustomerEmail} canceled their {ProductName} subscription",
            "Severity":        "Warning",
            "DurationSeconds": 8
          }
        ]
      }
    }
  }
}
```

`Severity` values: `Success`, `Info`, `Warning`, `Error`.

## Lazy localization

Webhooks arrive in a background thread with no HTTP request context — `CultureInfo.CurrentUICulture` is meaningless at dispatch time. `PolarToastNotification` stores the pre-rendered `en-US` fallback in `Message`/`Title` AND carries the raw `TokenValues` + localization keys. Call `Localize(localizer)` at UI render time where the culture IS set:

```csharp
await foreach (var raw in PolarToasts.Reader.ReadAllAsync(_cts.Token))
{
    var localized = raw.Localize(Localizer);   // uses CultureInfo.CurrentUICulture at THIS point
    ToastService.Show(localized.Title, localized.Message);
}
```

**Localization resolution order** (most specific wins):
1. `appsettings.json` `MessageTemplate` (host intentional customization)
2. Host app's custom `IPolarLocalizer`
3. PolarSharp built-in `.resx` (en-US and es-MX complete)
4. Pre-rendered `en-US` fallback (always present)

## Integration patterns

### Blazor Server + Telerik Notification Service

```razor
@implements IAsyncDisposable
@inject IPolarToastChannel PolarToasts
@inject INotificationService Notifications
@inject IPolarLocalizer Localizer

protected override Task OnInitializedAsync()
{
    _ = Task.Run(ListenAsync);
    return Task.CompletedTask;
}

private async Task ListenAsync()
{
    await foreach (var toast in PolarToasts.Reader.ReadAllAsync(_cts.Token))
    {
        var localized = toast.Localize(Localizer);
        await InvokeAsync(() =>
        {
            Notifications.Show(new NotificationModel
            {
                Text            = $"{localized.Title}: {localized.Message}",
                CloseAfterDelay = (int)localized.Duration.TotalMilliseconds,
            });
            StateHasChanged();
        });
    }
}

public async ValueTask DisposeAsync() => await _cts.CancelAsync();
private readonly CancellationTokenSource _cts = new();
```

### SignalR broadcast

```csharp
public sealed class PolarToastBroadcaster(IPolarToastChannel channel, IHubContext<AdminHub> hub)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var toast in channel.Reader.ReadAllAsync(ct))
            await hub.Clients.All.SendAsync("PolarToast", toast, ct);
    }
}
```

### Server-Sent Events (SSE)

```csharp
app.MapGet("/admin/events/toasts", async (IPolarToastChannel channel, CancellationToken ct) =>
    Results.Stream(async stream =>
    {
        await foreach (var toast in channel.Reader.ReadAllAsync(ct))
        {
            var json = JsonSerializer.Serialize(toast, PolarJsonContext.Default.PolarToastNotification);
            await stream.WriteAsync(Encoding.UTF8.GetBytes($"data: {json}\n\n"), ct);
            await stream.FlushAsync(ct);
        }
    }, "text/event-stream"));
```

## Available `{Tokens}` per event type

| Event type | Available tokens |
|---|---|
| `order.created`, `order.paid` | `{OrderNumber}`, `{OrderId}`, `{CustomerEmail}`, `{TotalAmount}`, `{Currency}`, `{ProductName}` |
| `subscription.active`, `subscription.canceled` | `{CustomerEmail}`, `{ProductName}`, `{PlanName}`, `{SubscriptionId}` |
| `customer.created`, `customer.updated` | `{CustomerEmail}`, `{CustomerName}`, `{CustomerId}` |
| `checkout.created`, `checkout.confirmed` | `{CustomerEmail}`, `{TotalAmount}`, `{Currency}` |
| `benefit.grant.created`, `benefit.grant.revoked` | `{CustomerEmail}`, `{BenefitType}`, `{ProductName}` |
| `refund.created`, `refund.updated` | `{OrderNumber}`, `{TotalAmount}`, `{Currency}`, `{RefundStatus}` |

Unknown `{Tokens}` that don't match are left as-is in the rendered string with a `Debug` log entry listing the unresolved tokens.
