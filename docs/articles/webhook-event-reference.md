# Webhook Event Reference

This article is the definitive per-event reference for every Polar webhook event type supported by `PolarSharp.Webhooks`. For each event you will find:

- The exact JSON payload Polar delivers
- The matching C# data model properties
- Practical usage examples (database persistence, email, logging, toast notifications)

> **Idempotency is mandatory.** Polar delivers webhooks at-least-once. The same `WebhookId` may arrive multiple times. Every handler **must** be idempotent — check whether the event has already been processed before performing side effects.

> **The standalone package.** `PolarSharp.Webhooks` does not require any other PolarSharp package. You can handle webhooks in a minimal receiver app without the full API client:
> ```bash
> dotnet add package PolarSharp.Webhooks
> ```

---

## How to read the examples

Each event section follows the same structure:

1. **Polar event type string** — the discriminator value in the `type` field
2. **C# handler skeleton** — the `HandleCoreAsync` override showing all available data fields
3. **Raw JSON example** — a realistic payload Polar actually sends
4. **Usage examples** — save to database · send email · log · toast notification

All handler classes inherit from `PolarWebhookHandlerBase<TEvent>` and are registered via `.AddWebhookHandler<TEvent, THandler>()`. The base class handles signature verification, DI scope, logging, and error wrapping before calling `HandleCoreAsync`.

---

## Base properties on every event

Every event inherits `WebhookEvent` which carries these properties regardless of event type:

| Property | Type | Source | Description |
|---|---|---|---|
| `Type` | `string` | JSON body `type` field | Discriminator string, e.g. `"order.created"` |
| `WebhookId` | `string` | `webhook-id` header | Stable across retries — use as idempotency key |
| `Timestamp` | `DateTimeOffset` | `webhook-timestamp` header | UTC delivery time injected after HMAC verification |
| `OrganizationId` | `string?` | Extracted pre-verification | Polar organization ID; used for multi-tenant routing |
| `ResolvedTenantId` | `string?` | Populated by `IWebhookTenantResolver` | Your app's tenant identifier, mapped from `OrganizationId` |

---

## Order events

### `order.created`

Fires when a customer places a new order on Polar (purchase, first subscription payment, etc.).

```csharp
public sealed class OrderCreatedHandler : PolarWebhookHandlerBase<OrderCreatedEvent>
{
    private readonly IOrderRepository _orders;
    private readonly IEmailSender _email;

    public OrderCreatedHandler(
        IOrderRepository orders,
        IEmailSender email,
        ILogger<OrderCreatedHandler> logger) : base(logger)
    {
        _orders = orders;
        _email  = email;
    }

    protected override async Task HandleCoreAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        var data = @event.Data;

        // ── Available data fields ──────────────────────────────────────────
        // data.Id              → "ord_01hwz..." (Polar order ID — use as idempotency key)
        // data.Number          → "1042" (human-readable order reference)
        // data.Status          → "paid" | "pending" | "refunded" | "partially_refunded"
        // data.Amount          → 2999 (total in minor currency units — $29.99 if USD)
        // data.TaxAmount       → 240 (tax portion in minor units)
        // data.Currency        → "USD" (ISO 4217)
        // data.Channel         → "web" | "api" | "embed" (how the order was placed)
        // data.BillingReason   → "purchase" | "renewal" (subscription renewal vs first purchase)
        // data.OrganizationId  → "org_01hwz..." (your Polar org)
        // data.SubscriptionId  → "sub_01hwz..." | null (set only on subscription renewal orders)
        // data.CreatedAt       → DateTimeOffset (UTC)
        // data.Customer.Id     → "cus_01hwz..." (Polar customer ID)
        // data.Customer.Email  → "alice@example.com"
        // data.Customer.Name   → "Alice Smith" | null
        // data.Items           → IReadOnlyList<WebhookOrderItem>
        //   item.ProductId     → "prod_01hwz..."
        //   item.ProductName   → "Pro Plan"
        //   item.PriceAmount   → 2999 (unit price in minor units)
        //   item.Currency      → "USD"

        // ── 1. Idempotency check ───────────────────────────────────────────
        var alreadyProcessed = await _orders.ExistsAsync(@event.WebhookId, ct);
        if (alreadyProcessed) return;

        // ── 2. Persist to database ────────────────────────────────────────
        await _orders.CreateAsync(new Order
        {
            WebhookId    = @event.WebhookId,
            PolarOrderId = data.Id,
            OrderNumber  = data.Number ?? "",
            CustomerEmail = data.Customer?.Email ?? "",
            TotalCents   = data.Amount ?? 0,
            TaxCents     = data.TaxAmount ?? 0,
            Currency     = data.Currency ?? "USD",
            Status       = data.Status ?? "pending",
            Channel      = data.Channel ?? "web",
            IsRenewal    = data.BillingReason == "renewal",
            Items = data.Items.Select(i => new OrderLineItem
            {
                ProductName = i.ProductName ?? "",
                PriceCents  = i.PriceAmount ?? 0,
            }).ToList(),
            CreatedAt = data.CreatedAt ?? @event.Timestamp,
        }, ct);

        // ── 3. Send confirmation email ────────────────────────────────────
        if (data.Customer?.Email is { Length: > 0 } email)
        {
            await _email.SendOrderConfirmationAsync(
                to:          email,
                customerName: data.Customer.Name,
                orderNumber: data.Number,
                totalCents:  data.Amount ?? 0,
                currency:    data.Currency ?? "USD",
                items:       data.Items,
                ct:          ct);
        }
    }
}
```

**Raw JSON payload:**
```json
{
  "type": "order.created",
  "data": {
    "id": "ord_01hwz3abc123",
    "status": "paid",
    "number": "1042",
    "amount": 2999,
    "tax_amount": 240,
    "currency": "USD",
    "channel": "web",
    "billing_reason": "purchase",
    "organization_id": "org_01hwz3org456",
    "subscription_id": null,
    "created_at": "2026-05-12T14:22:01Z",
    "customer": {
      "id": "cus_01hwz3cus789",
      "email": "alice@example.com",
      "name": "Alice Smith"
    },
    "items": [
      {
        "product_id": "prod_01hwz3prd001",
        "product_name": "Pro Plan",
        "price_amount": 2999,
        "currency": "USD"
      }
    ]
  }
}
```

---

### `order.updated`

Fires when an existing order is modified (status change, partial refund applied, etc.).

```csharp
protected override async Task HandleCoreAsync(OrderUpdatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // data fields are identical to order.created — see above.
    // Common reason to handle: status changed from "pending" → "paid",
    // or "paid" → "partially_refunded".

    await _orders.UpsertStatusAsync(
        polarOrderId: data.Id,
        newStatus:    data.Status ?? "pending",
        webhookId:    @event.WebhookId,
        ct:           ct);
}
```

---

### `order.paid`

Fires when payment is successfully captured. This is the primary fulfillment trigger for one-time purchases.

```csharp
protected override async Task HandleCoreAsync(OrderPaidEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // ── Idempotency ──
    if (await _orders.IsFulfilledAsync(@event.WebhookId, ct)) return;

    // ── Provision access ──
    // data.Customer.Id is the stable customer ID to grant entitlements against.
    await _entitlements.GrantAsync(
        customerId:  data.Customer?.Id ?? throw new InvalidOperationException("No customer"),
        productId:   data.Items.FirstOrDefault()?.ProductId ?? "",
        ct:          ct);

    // ── Mark fulfilled ──
    await _orders.MarkFulfilledAsync(data.Id, @event.WebhookId, ct);

    // ── Log (structured) ──
    logger.LogInformation(
        "Order {OrderNumber} paid by {CustomerEmail} — {Amount} {Currency}",
        data.Number, data.Customer?.Email, data.Amount, data.Currency);
}
```

**Raw JSON payload:**
```json
{
  "type": "order.paid",
  "data": {
    "id": "ord_01hwz3abc123",
    "status": "paid",
    "number": "1042",
    "amount": 2999,
    "tax_amount": 240,
    "currency": "USD",
    "channel": "web",
    "billing_reason": "purchase",
    "organization_id": "org_01hwz3org456",
    "subscription_id": null,
    "created_at": "2026-05-12T14:22:01Z",
    "customer": {
      "id": "cus_01hwz3cus789",
      "email": "alice@example.com",
      "name": "Alice Smith"
    },
    "items": [
      {
        "product_id": "prod_01hwz3prd001",
        "product_name": "Pro Plan",
        "price_amount": 2999,
        "currency": "USD"
      }
    ]
  }
}
```

---

### `order.refunded`

Fires when a full or partial refund is processed on an order.

```csharp
protected override async Task HandleCoreAsync(OrderRefundedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // data.Status will be "refunded" (full) or "partially_refunded" (partial)
    // data.Amount reflects the remaining charged amount after the refund.
    // Check the companion refund.created event for the refund amount itself.

    await _orders.UpdateStatusAsync(data.Id, data.Status ?? "refunded", ct);

    if (data.Status == "refunded")
    {
        // Full refund — revoke access if applicable
        await _entitlements.RevokeAsync(data.Customer?.Id ?? "", data.Id, ct);
    }

    logger.LogInformation(
        "Order {OrderNumber} {Status} — customer {Email}",
        data.Number, data.Status, data.Customer?.Email);
}
```

---

## Subscription events

### `subscription.created`

Fires when a customer starts a new subscription (before the first payment clears).

```csharp
protected override async Task HandleCoreAsync(SubscriptionCreatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // ── Available data fields ──────────────────────────────────────────
    // data.Id                    → "sub_01hwz..." (Polar subscription ID)
    // data.Status                → "incomplete" | "active" | "past_due" | "canceled" | "unpaid"
    // data.CurrentPeriodStart    → DateTimeOffset (UTC billing period start)
    // data.CurrentPeriodEnd      → DateTimeOffset (UTC billing period end — null if ongoing)
    // data.CanceledAt            → DateTimeOffset? (set when canceled)
    // data.EndsAt                → DateTimeOffset? (when access expires after cancellation)
    // data.OrganizationId        → "org_01hwz..."
    // data.CreatedAt             → DateTimeOffset
    // data.Customer.Id           → "cus_01hwz..."
    // data.Customer.Email        → "alice@example.com"
    // data.Customer.Name         → "Alice Smith" | null
    // data.Product.Id            → "prod_01hwz..."
    // data.Product.Name          → "Pro Plan"
    // data.Price.Id              → "price_01hwz..."
    // data.Price.Name            → "Monthly" | "Annual"
    // data.Price.Amount          → 2999 (monthly price in minor units)
    // data.Price.Currency        → "USD"

    await _subscriptions.CreateAsync(new Subscription
    {
        WebhookId      = @event.WebhookId,
        PolarSubId     = data.Id,
        CustomerId     = data.Customer?.Id ?? "",
        CustomerEmail  = data.Customer?.Email ?? "",
        ProductId      = data.Product?.Id ?? "",
        ProductName    = data.Product?.Name ?? "",
        PlanName       = data.Price?.Name ?? "",
        PriceCents     = data.Price?.Amount ?? 0,
        Currency       = data.Price?.Currency ?? "USD",
        Status         = data.Status ?? "incomplete",
        PeriodStart    = data.CurrentPeriodStart ?? DateTimeOffset.UtcNow,
        PeriodEnd      = data.CurrentPeriodEnd,
        CreatedAt      = data.CreatedAt ?? @event.Timestamp,
    }, ct);
}
```

**Raw JSON payload:**
```json
{
  "type": "subscription.created",
  "data": {
    "id": "sub_01hwz3sub001",
    "status": "incomplete",
    "current_period_start": "2026-05-12T00:00:00Z",
    "current_period_end": "2026-06-12T00:00:00Z",
    "canceled_at": null,
    "ends_at": null,
    "organization_id": "org_01hwz3org456",
    "created_at": "2026-05-12T14:22:00Z",
    "customer": {
      "id": "cus_01hwz3cus789",
      "email": "alice@example.com",
      "name": "Alice Smith"
    },
    "product": {
      "id": "prod_01hwz3prd001",
      "name": "Pro Plan"
    },
    "price": {
      "id": "price_01hwz3prc001",
      "name": "Monthly",
      "price_amount": 2999,
      "price_currency": "USD"
    }
  }
}
```

---

### `subscription.active`

Fires when a subscription transitions to `active` status — typically after the first payment clears. This is the trigger for provisioning recurring access.

```csharp
protected override async Task HandleCoreAsync(SubscriptionActiveEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    if (await _subscriptions.IsActiveAsync(@event.WebhookId, ct)) return;

    // Grant the recurring benefit
    await _entitlements.GrantSubscriptionAsync(
        customerId:  data.Customer?.Id ?? "",
        productId:   data.Product?.Id ?? "",
        subId:       data.Id,
        endsAt:      data.CurrentPeriodEnd,
        ct:          ct);

    await _subscriptions.SetStatusAsync(data.Id, "active", data.CurrentPeriodEnd, ct);

    logger.LogInformation(
        "Subscription {SubId} active — {Email} on {Plan} ({PlanName})",
        data.Id, data.Customer?.Email, data.Product?.Name, data.Price?.Name);
}
```

---

### `subscription.updated`

Fires on any subscription field change — plan upgrade/downgrade, billing period renewal, payment method update, etc.

```csharp
protected override async Task HandleCoreAsync(SubscriptionUpdatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // Always upsert — this event may fire many times over the subscription lifetime.
    await _subscriptions.UpsertAsync(new SubscriptionUpdate
    {
        PolarSubId  = data.Id,
        WebhookId   = @event.WebhookId,
        Status      = data.Status ?? "",
        PeriodStart = data.CurrentPeriodStart,
        PeriodEnd   = data.CurrentPeriodEnd,
        PlanName    = data.Price?.Name ?? "",
        PriceCents  = data.Price?.Amount ?? 0,
    }, ct);
}
```

---

### `subscription.canceled`

Fires when a customer cancels. The subscription typically remains `active` until `EndsAt` — access should not be revoked immediately.

```csharp
protected override async Task HandleCoreAsync(SubscriptionCanceledEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // data.CanceledAt  — when the cancel request was received
    // data.EndsAt      — when access actually expires (end of paid period)
    // data.Status      — usually still "active" at this point; becomes "canceled" at period end

    await _subscriptions.MarkCanceledAsync(
        polarSubId:  data.Id,
        canceledAt:  data.CanceledAt ?? @event.Timestamp,
        endsAt:      data.EndsAt,
        ct:          ct);

    // Notify the customer (do NOT revoke access yet — they paid through the period end)
    if (data.Customer?.Email is { Length: > 0 } email)
    {
        await _email.SendCancellationAcknowledgmentAsync(
            to:     email,
            endsAt: data.EndsAt,
            ct:     ct);
    }

    logger.LogInformation(
        "Subscription {SubId} canceled by {Email} — access ends {EndsAt}",
        data.Id, data.Customer?.Email, data.EndsAt);
}
```

**Raw JSON payload:**
```json
{
  "type": "subscription.canceled",
  "data": {
    "id": "sub_01hwz3sub001",
    "status": "active",
    "current_period_start": "2026-05-12T00:00:00Z",
    "current_period_end": "2026-06-12T00:00:00Z",
    "canceled_at": "2026-05-12T18:45:00Z",
    "ends_at": "2026-06-12T00:00:00Z",
    "organization_id": "org_01hwz3org456",
    "created_at": "2026-04-12T00:00:00Z",
    "customer": {
      "id": "cus_01hwz3cus789",
      "email": "alice@example.com",
      "name": "Alice Smith"
    },
    "product": {
      "id": "prod_01hwz3prd001",
      "name": "Pro Plan"
    },
    "price": {
      "id": "price_01hwz3prc001",
      "name": "Monthly",
      "price_amount": 2999,
      "price_currency": "USD"
    }
  }
}
```

---

### `subscription.uncanceled`

Fires when a customer reactivates a subscription that was previously canceled but has not yet expired.

```csharp
protected override async Task HandleCoreAsync(SubscriptionUncanceledEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // data.CanceledAt will be null — cancellation was reversed
    // data.EndsAt will be null — subscription continues indefinitely

    await _subscriptions.ClearCancellationAsync(data.Id, ct);

    logger.LogInformation(
        "Subscription {SubId} reactivated by {Email}",
        data.Id, data.Customer?.Email);
}
```

---

### `subscription.past_due`

Fires when a recurring payment fails and the subscription enters `past_due` status. Polar will retry the charge — do not immediately revoke access.

```csharp
protected override async Task HandleCoreAsync(SubscriptionPastDueEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    await _subscriptions.SetStatusAsync(data.Id, "past_due", null, ct);

    // Warn the customer; Polar will retry the charge automatically
    if (data.Customer?.Email is { Length: > 0 } email)
    {
        await _email.SendPaymentFailedWarningAsync(
            to:          email,
            productName: data.Product?.Name ?? "",
            ct:          ct);
    }

    logger.LogWarning(
        "Subscription {SubId} past due — {Email}. Polar will retry.",
        data.Id, data.Customer?.Email);
}
```

---

### `subscription.revoked`

Fires when a subscription is hard-revoked — access must be removed immediately. This occurs when all payment retries are exhausted or an admin explicitly revokes the subscription.

```csharp
protected override async Task HandleCoreAsync(SubscriptionRevokedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // data.Status will be "unpaid" or "canceled"
    // Revoke access immediately — no grace period applies here.

    await _entitlements.RevokeSubscriptionAsync(data.Customer?.Id ?? "", data.Id, ct);
    await _subscriptions.SetStatusAsync(data.Id, data.Status ?? "unpaid", null, ct);

    logger.LogWarning(
        "Subscription {SubId} revoked for {Email} — reason: {Status}",
        data.Id, data.Customer?.Email, data.Status);
}
```

**Raw JSON payload:**
```json
{
  "type": "subscription.revoked",
  "data": {
    "id": "sub_01hwz3sub001",
    "status": "unpaid",
    "current_period_start": "2026-05-12T00:00:00Z",
    "current_period_end": null,
    "canceled_at": null,
    "ends_at": null,
    "organization_id": "org_01hwz3org456",
    "created_at": "2026-04-12T00:00:00Z",
    "customer": {
      "id": "cus_01hwz3cus789",
      "email": "alice@example.com",
      "name": "Alice Smith"
    },
    "product": {
      "id": "prod_01hwz3prd001",
      "name": "Pro Plan"
    },
    "price": {
      "id": "price_01hwz3prc001",
      "name": "Monthly",
      "price_amount": 2999,
      "price_currency": "USD"
    }
  }
}
```

---

## Checkout events

### `checkout.created`

Fires when a customer initiates a Polar checkout session (before payment is entered).

```csharp
protected override async Task HandleCoreAsync(CheckoutCreatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // ── Available data fields ──────────────────────────────────────────
    // data.Id               → "chk_01hwz..." (checkout session ID)
    // data.Status           → "open" | "confirmed" | "expired"
    // data.Amount           → 2999 (total in minor units)
    // data.Currency         → "USD"
    // data.CustomerEmail    → "alice@example.com" | null (null until customer enters email)
    // data.Customer.Id      → "cus_01hwz..." | null (null until customer is identified)
    // data.Customer.Email   → "alice@example.com" | null
    // data.Customer.Name    → null (rarely set at checkout creation)
    // data.ExpiresAt        → DateTimeOffset (when the session expires)
    // data.OrderId          → null (populated only after checkout.confirmed)
    // data.OrganizationId   → "org_01hwz..."
    // data.CreatedAt        → DateTimeOffset

    // Track the checkout for analytics (e.g., abandoned checkout funnel)
    await _analytics.TrackCheckoutStartedAsync(
        checkoutId:    data.Id,
        customerEmail: data.CustomerEmail ?? data.Customer?.Email,
        amountCents:   data.Amount ?? 0,
        currency:      data.Currency ?? "USD",
        expiresAt:     data.ExpiresAt ?? @event.Timestamp.AddHours(1),
        ct:            ct);
}
```

**Raw JSON payload:**
```json
{
  "type": "checkout.created",
  "data": {
    "id": "chk_01hwz3chk001",
    "status": "open",
    "amount": 2999,
    "currency": "USD",
    "customer_email": null,
    "customer": null,
    "expires_at": "2026-05-12T15:22:01Z",
    "order_id": null,
    "organization_id": "org_01hwz3org456",
    "created_at": "2026-05-12T14:22:01Z"
  }
}
```

---

### `checkout.updated`

Fires when checkout fields change — customer email entered, product quantities changed, etc.

```csharp
protected override async Task HandleCoreAsync(CheckoutUpdatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // Update the email if the customer has now entered it (abandoned-cart recovery)
    if (data.CustomerEmail is { Length: > 0 } email)
    {
        await _analytics.UpdateCheckoutEmailAsync(data.Id, email, ct);
    }

    // When status → "confirmed", the companion order.created event also fires.
    // Handle fulfillment in order.paid, not here.
}
```

---

### `checkout.expired`

Fires when a checkout session expires without completing. Use this to trigger abandoned-cart recovery campaigns.

```csharp
protected override async Task HandleCoreAsync(CheckoutExpiredEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // data.Status will be "expired"

    await _analytics.MarkCheckoutExpiredAsync(data.Id, ct);

    // Abandoned-cart email (only if customer entered their email before leaving)
    var email = data.CustomerEmail ?? data.Customer?.Email;
    if (email is { Length: > 0 })
    {
        await _email.SendAbandonedCartAsync(
            to:          email,
            amountCents: data.Amount ?? 0,
            currency:    data.Currency ?? "USD",
            ct:          ct);
    }

    logger.LogInformation(
        "Checkout {CheckoutId} expired — {Email}",
        data.Id, email ?? "(no email captured)");
}
```

**Raw JSON payload:**
```json
{
  "type": "checkout.expired",
  "data": {
    "id": "chk_01hwz3chk001",
    "status": "expired",
    "amount": 2999,
    "currency": "USD",
    "customer_email": "alice@example.com",
    "customer": null,
    "expires_at": "2026-05-12T15:22:01Z",
    "order_id": null,
    "organization_id": "org_01hwz3org456",
    "created_at": "2026-05-12T14:22:01Z"
  }
}
```

---

## Customer events

### `customer.created`

Fires when a new customer record is created in Polar (typically at first purchase or checkout start).

```csharp
protected override async Task HandleCoreAsync(CustomerCreatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // ── Available data fields ──────────────────────────────────────────
    // data.Id                        → "cus_01hwz..." (stable Polar customer ID)
    // data.Email                     → "alice@example.com"
    // data.Name                      → "Alice Smith" | null
    // data.OrganizationId            → "org_01hwz..."
    // data.ActiveSubscriptionsCount  → 0 (at creation, always 0)
    // data.CreatedAt                 → DateTimeOffset

    if (await _customers.ExistsAsync(@event.WebhookId, ct)) return;

    await _customers.CreateAsync(new Customer
    {
        WebhookId       = @event.WebhookId,
        PolarCustomerId = data.Id,
        Email           = data.Email ?? "",
        Name            = data.Name,
        OrganizationId  = data.OrganizationId,
        CreatedAt       = data.CreatedAt ?? @event.Timestamp,
    }, ct);

    // Welcome email
    if (data.Email is { Length: > 0 } email)
    {
        await _email.SendWelcomeAsync(email, data.Name, ct);
    }
}
```

**Raw JSON payload:**
```json
{
  "type": "customer.created",
  "data": {
    "id": "cus_01hwz3cus789",
    "email": "alice@example.com",
    "name": "Alice Smith",
    "organization_id": "org_01hwz3org456",
    "active_subscriptions_count": 0,
    "created_at": "2026-05-12T14:22:00Z"
  }
}
```

---

### `customer.updated`

Fires when a customer's profile fields change (name, email, etc.).

```csharp
protected override async Task HandleCoreAsync(CustomerUpdatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    await _customers.UpdateProfileAsync(
        polarCustomerId: data.Id,
        email:           data.Email,
        name:            data.Name,
        ct:              ct);

    logger.LogInformation(
        "Customer {CustomerId} profile updated — new email: {Email}",
        data.Id, data.Email);
}
```

---

### `customer.state_changed`

Fires when the customer's subscription state changes — active subscription count goes from 0→1 (first subscriber) or 1→0 (churned). Use this to manage coarse-grained access tiers.

```csharp
protected override async Task HandleCoreAsync(CustomerStateChangedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // data.ActiveSubscriptionsCount → current active subscription count
    var isActive = (data.ActiveSubscriptionsCount ?? 0) > 0;

    await _customers.SetActiveAsync(data.Id, isActive, ct);

    if (!isActive)
    {
        // Customer churned — all subscriptions have ended
        logger.LogInformation(
            "Customer {CustomerId} ({Email}) has no active subscriptions",
            data.Id, data.Email);
    }
    else
    {
        logger.LogInformation(
            "Customer {CustomerId} ({Email}) now has {Count} active subscription(s)",
            data.Id, data.Email, data.ActiveSubscriptionsCount);
    }
}
```

**Raw JSON payload:**
```json
{
  "type": "customer.state_changed",
  "data": {
    "id": "cus_01hwz3cus789",
    "email": "alice@example.com",
    "name": "Alice Smith",
    "organization_id": "org_01hwz3org456",
    "active_subscriptions_count": 1,
    "created_at": "2026-05-12T14:22:00Z"
  }
}
```

---

### `customer.deleted`

Fires when a customer account is deleted from Polar (e.g., GDPR deletion request).

```csharp
protected override async Task HandleCoreAsync(CustomerDeletedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // Anonymize or hard-delete customer PII in your own database.
    // Polar has already deleted their record — you must honor the deletion.
    await _customers.AnonymizeAsync(data.Id, ct);
    await _entitlements.RevokeAllAsync(data.Id, ct);

    logger.LogInformation(
        "Customer {CustomerId} deleted from Polar — PII anonymized locally",
        data.Id);
}
```

---

## Product events

### `product.created`

Fires when a new product is created in your Polar organization.

```csharp
protected override async Task HandleCoreAsync(ProductCreatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // ── Available data fields ──────────────────────────────────────────
    // data.Id             → "prod_01hwz..." (Polar product ID)
    // data.Name           → "Pro Plan"
    // data.IsArchived     → false (always false on creation)
    // data.OrganizationId → "org_01hwz..."
    // data.CreatedAt      → DateTimeOffset

    // Mirror the product catalog locally (for display without API calls)
    await _products.UpsertAsync(new Product
    {
        PolarProductId = data.Id,
        Name           = data.Name ?? "",
        IsArchived     = data.IsArchived,
        CreatedAt      = data.CreatedAt ?? @event.Timestamp,
    }, ct);
}
```

**Raw JSON payload:**
```json
{
  "type": "product.created",
  "data": {
    "id": "prod_01hwz3prd001",
    "name": "Pro Plan",
    "is_archived": false,
    "organization_id": "org_01hwz3org456",
    "created_at": "2026-05-12T10:00:00Z"
  }
}
```

---

### `product.updated`

Fires when a product is renamed, archived, or otherwise modified.

```csharp
protected override async Task HandleCoreAsync(ProductUpdatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    await _products.UpsertAsync(new Product
    {
        PolarProductId = data.Id,
        Name           = data.Name ?? "",
        IsArchived     = data.IsArchived,
    }, ct);

    if (data.IsArchived)
    {
        // Prevent new purchases; existing subscribers unaffected
        logger.LogInformation("Product {ProductId} ({Name}) archived", data.Id, data.Name);
    }
}
```

---

## Benefit events

### `benefit.created`

Fires when a new benefit is defined in your Polar organization (e.g., a Discord role grant, a license key entitlement, or a custom webhook benefit).

```csharp
protected override async Task HandleCoreAsync(BenefitCreatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // ── Available data fields ──────────────────────────────────────────
    // data.Id           → "ben_01hwz..." (Polar benefit ID)
    // data.BenefitType  → "custom" | "discord" | "license_keys" | "downloadables" | "github_repository"
    // data.Description  → "Discord community access" (human-readable label)
    // data.OrganizationId → "org_01hwz..."
    // data.CreatedAt    → DateTimeOffset

    await _benefits.UpsertAsync(new Benefit
    {
        PolarBenefitId = data.Id,
        Type           = data.BenefitType ?? "custom",
        Description    = data.Description ?? "",
        CreatedAt      = data.CreatedAt ?? @event.Timestamp,
    }, ct);

    logger.LogInformation(
        "Benefit {BenefitId} created — type: {Type}, description: {Description}",
        data.Id, data.BenefitType, data.Description);
}
```

**Raw JSON payload:**
```json
{
  "type": "benefit.created",
  "data": {
    "id": "ben_01hwz3ben001",
    "type": "discord",
    "description": "Discord community access",
    "organization_id": "org_01hwz3org456",
    "created_at": "2026-05-12T09:00:00Z"
  }
}
```

---

### `benefit.updated`

Fires when a benefit's description or configuration changes.

```csharp
protected override async Task HandleCoreAsync(BenefitUpdatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    await _benefits.UpsertAsync(new Benefit
    {
        PolarBenefitId = data.Id,
        Type           = data.BenefitType ?? "custom",
        Description    = data.Description ?? "",
    }, ct);
}
```

---

## Benefit grant events

Benefit grants represent a specific customer receiving a specific benefit. They are created automatically when an order or subscription activates a product that includes the benefit.

### `benefit_grant.created`

Fires when Polar assigns a benefit to a customer. Provision the actual access — invite to Discord, activate a license key, etc.

```csharp
protected override async Task HandleCoreAsync(BenefitGrantCreatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // ── Available data fields ──────────────────────────────────────────
    // data.Id             → "bng_01hwz..." (benefit grant ID)
    // data.CustomerId     → "cus_01hwz..." (the beneficiary)
    // data.BenefitId      → "ben_01hwz..." (the benefit being granted)
    // data.BenefitType    → "discord" | "license_keys" | "custom" | "downloadables"
    // data.IsGranted      → true (always true on creation)
    // data.GrantedAt      → DateTimeOffset (when the grant was awarded)
    // data.RevokedAt      → null (not yet revoked)
    // data.OrganizationId → "org_01hwz..."

    if (await _grants.ExistsAsync(@event.WebhookId, ct)) return;

    await _grants.CreateAsync(new BenefitGrant
    {
        WebhookId   = @event.WebhookId,
        GrantId     = data.Id,
        CustomerId  = data.CustomerId ?? "",
        BenefitId   = data.BenefitId ?? "",
        BenefitType = data.BenefitType ?? "",
        IsGranted   = true,
        GrantedAt   = data.GrantedAt ?? @event.Timestamp,
    }, ct);

    // Type-specific provisioning
    switch (data.BenefitType)
    {
        case "discord":
            await _discord.InviteToServerAsync(data.CustomerId ?? "", ct);
            break;
        case "license_keys":
            await _licenses.ActivateKeyForCustomerAsync(data.CustomerId ?? "", data.BenefitId ?? "", ct);
            break;
        case "github_repository":
            await _github.AddCollaboratorAsync(data.CustomerId ?? "", ct);
            break;
    }
}
```

**Raw JSON payload:**
```json
{
  "type": "benefit_grant.created",
  "data": {
    "id": "bng_01hwz3bng001",
    "customer_id": "cus_01hwz3cus789",
    "benefit_id": "ben_01hwz3ben001",
    "benefit_type": "discord",
    "is_granted": true,
    "granted_at": "2026-05-12T14:22:05Z",
    "revoked_at": null,
    "organization_id": "org_01hwz3org456"
  }
}
```

---

### `benefit_grant.updated`

Fires when a grant's state changes. Check `IsGranted` to determine whether access was granted or revoked.

```csharp
protected override async Task HandleCoreAsync(BenefitGrantUpdatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    await _grants.UpdateAsync(data.Id, data.IsGranted, data.RevokedAt, ct);

    if (!data.IsGranted)
    {
        // Benefit was revoked mid-grant — remove access immediately
        await RevokeAccessAsync(data.BenefitType ?? "", data.CustomerId ?? "", ct);

        logger.LogInformation(
            "Benefit grant {GrantId} revoked for customer {CustomerId} at {RevokedAt}",
            data.Id, data.CustomerId, data.RevokedAt);
    }
}
```

---

### `benefit_grant.cycled`

Fires when a benefit grant is refreshed (e.g., a license key is rotated). Update the stored access details.

```csharp
protected override async Task HandleCoreAsync(BenefitGrantCycledEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // A "cycled" grant is still active — the underlying credential was rotated.
    // Re-fetch the latest grant properties from Polar if you need the new credential value.
    await _grants.MarkCycledAsync(data.Id, @event.Timestamp, ct);

    logger.LogInformation(
        "Benefit grant {GrantId} cycled for customer {CustomerId}",
        data.Id, data.CustomerId);
}
```

---

### `benefit_grant.revoked`

Fires when Polar definitively revokes a benefit. Remove access immediately.

```csharp
protected override async Task HandleCoreAsync(BenefitGrantRevokedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // data.IsGranted will be false
    // data.RevokedAt will be set

    await _grants.RevokeAsync(data.Id, data.RevokedAt ?? @event.Timestamp, ct);

    switch (data.BenefitType)
    {
        case "discord":
            await _discord.RemoveFromServerAsync(data.CustomerId ?? "", ct);
            break;
        case "license_keys":
            await _licenses.DeactivateKeyAsync(data.CustomerId ?? "", data.BenefitId ?? "", ct);
            break;
        case "github_repository":
            await _github.RemoveCollaboratorAsync(data.CustomerId ?? "", ct);
            break;
    }

    logger.LogInformation(
        "Benefit grant {GrantId} ({Type}) revoked for customer {CustomerId}",
        data.Id, data.BenefitType, data.CustomerId);
}
```

**Raw JSON payload:**
```json
{
  "type": "benefit_grant.revoked",
  "data": {
    "id": "bng_01hwz3bng001",
    "customer_id": "cus_01hwz3cus789",
    "benefit_id": "ben_01hwz3ben001",
    "benefit_type": "discord",
    "is_granted": false,
    "granted_at": "2026-05-12T14:22:05Z",
    "revoked_at": "2026-06-12T00:00:00Z",
    "organization_id": "org_01hwz3org456"
  }
}
```

---

## Refund events

### `refund.created`

Fires when a refund is initiated on an order.

```csharp
protected override async Task HandleCoreAsync(RefundCreatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // ── Available data fields ──────────────────────────────────────────
    // data.Id             → "ref_01hwz..." (Polar refund ID)
    // data.Amount         → 2999 (refund amount in minor units)
    // data.Currency       → "USD"
    // data.Reason         → "customer_request" | "duplicate" | "fraudulent" | "other"
    // data.Status         → "pending" | "succeeded" | "failed" | "canceled"
    // data.OrderId        → "ord_01hwz..." (the order being refunded)
    // data.CustomerId     → "cus_01hwz..." (the customer receiving the refund)
    // data.OrganizationId → "org_01hwz..."
    // data.CreatedAt      → DateTimeOffset

    if (await _refunds.ExistsAsync(@event.WebhookId, ct)) return;

    await _refunds.CreateAsync(new Refund
    {
        WebhookId   = @event.WebhookId,
        PolarRefundId = data.Id,
        PolarOrderId  = data.OrderId ?? "",
        CustomerId    = data.CustomerId ?? "",
        AmountCents   = data.Amount ?? 0,
        Currency      = data.Currency ?? "USD",
        Reason        = data.Reason ?? "other",
        Status        = data.Status ?? "pending",
        CreatedAt     = data.CreatedAt ?? @event.Timestamp,
    }, ct);

    // Notify the customer
    if (data.CustomerId is { Length: > 0 })
    {
        var email = await _customers.GetEmailAsync(data.CustomerId, ct);
        if (email is not null)
        {
            await _email.SendRefundInitiatedAsync(
                to:          email,
                amountCents: data.Amount ?? 0,
                currency:    data.Currency ?? "USD",
                ct:          ct);
        }
    }

    logger.LogInformation(
        "Refund {RefundId} created — {Amount} {Currency} for order {OrderId}, reason: {Reason}",
        data.Id, data.Amount, data.Currency, data.OrderId, data.Reason);
}
```

**Raw JSON payload:**
```json
{
  "type": "refund.created",
  "data": {
    "id": "ref_01hwz3ref001",
    "amount": 2999,
    "currency": "USD",
    "reason": "customer_request",
    "status": "pending",
    "order_id": "ord_01hwz3abc123",
    "customer_id": "cus_01hwz3cus789",
    "organization_id": "org_01hwz3org456",
    "created_at": "2026-05-12T16:00:00Z"
  }
}
```

---

### `refund.updated`

Fires when the refund status changes (`pending` → `succeeded` | `failed` | `canceled`).

```csharp
protected override async Task HandleCoreAsync(RefundUpdatedEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    await _refunds.UpdateStatusAsync(data.Id, data.Status ?? "pending", ct);

    switch (data.Status)
    {
        case "succeeded":
            // Refund confirmed — revoke access if this was a full refund
            logger.LogInformation(
                "Refund {RefundId} succeeded — {Amount} {Currency} returned to customer",
                data.Id, data.Amount, data.Currency);
            break;
        case "failed":
            // Refund could not be processed — no action needed for access
            logger.LogWarning(
                "Refund {RefundId} failed — manual review may be required",
                data.Id);
            break;
    }
}
```

**Raw JSON payload:**
```json
{
  "type": "refund.updated",
  "data": {
    "id": "ref_01hwz3ref001",
    "amount": 2999,
    "currency": "USD",
    "reason": "customer_request",
    "status": "succeeded",
    "order_id": "ord_01hwz3abc123",
    "customer_id": "cus_01hwz3cus789",
    "organization_id": "org_01hwz3org456",
    "created_at": "2026-05-12T16:00:00Z"
  }
}
```

---

## Toast notification integration

Every handler's side effect can be mirrored to the admin UI via `IPolarToastChannel`. Configure the whitelist in `appsettings.json`:

```json
{
  "PolarSharp": {
    "Webhooks": {
      "ToastNotifications": {
        "Enabled": true,
        "ChannelCapacity": 100,
        "Events": [
          {
            "EventType":       "order.paid",
            "Title":           "New Sale",
            "MessageTemplate": "Order #{OrderNumber} — {Amount} {Currency} from {CustomerEmail}",
            "Severity":        "Success",
            "DurationSeconds": 5
          },
          {
            "EventType":       "subscription.active",
            "Title":           "New Subscriber",
            "MessageTemplate": "{CustomerEmail} subscribed to {ProductName} ({PlanName})",
            "Severity":        "Success",
            "DurationSeconds": 5
          },
          {
            "EventType":       "subscription.canceled",
            "Title":           "Cancellation",
            "MessageTemplate": "{CustomerEmail} canceled {ProductName}",
            "Severity":        "Warning",
            "DurationSeconds": 8
          },
          {
            "EventType":       "refund.created",
            "Title":           "Refund Requested",
            "MessageTemplate": "{Amount} {Currency} refund for order #{OrderNumber}",
            "Severity":        "Info",
            "DurationSeconds": 5
          }
        ]
      }
    }
  }
}
```

Consume in a Blazor admin layout:

```csharp
// AdminLayout.razor
@implements IAsyncDisposable
@inject IPolarToastChannel PolarToasts
@inject INotificationService Notifications  // e.g. Telerik
@inject IStringLocalizer<PolarWebhookMessages> Localizer

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

---

## Standalone registration pattern

When using `PolarSharp.Webhooks` without the full `PolarSharp` API client:

```csharp
// Program.cs — webhook-receiver-only service
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPolarWebhooks()                                             // ← no other PolarSharp package needed
    .AddWebhookHandler<OrderPaidEvent, OrderPaidHandler>()
    .AddWebhookHandler<SubscriptionActiveEvent, SubscriptionActiveHandler>()
    .AddWebhookHandler<SubscriptionCanceledEvent, SubscriptionCanceledHandler>()
    .AddWebhookHandler<RefundCreatedEvent, RefundCreatedHandler>();

var app = builder.Build();
app.MapPolarWebhooks();   // registers the POST /hooks/polar endpoint
app.Run();
```

Required `appsettings.json` minimum:

```json
{
  "PolarSharp": {
    "Webhooks": {
      "Secret": "whsec_your_secret_here",
      "Path":   "/hooks/polar"
    }
  }
}
```

---

## Token amounts: minor currency units

All `Amount` and `PriceAmount` fields are integers in **minor currency units** (cents for USD/EUR, pence for GBP, etc.). Convert for display:

```csharp
// Extension method for display
public static string FormatAmount(int? amountMinorUnits, string? currency)
{
    if (amountMinorUnits is null || currency is null) return "—";
    var amount = amountMinorUnits.Value / 100m;
    return amount.ToString("C", new System.Globalization.CultureInfo(
        currency == "USD" ? "en-US" :
        currency == "EUR" ? "fr-FR" :
        currency == "GBP" ? "en-GB" : "en-US"));
}

// Usage in handler:
var display = FormatAmount(data.Amount, data.Currency);  // → "$29.99"
```

---

## Recommended handler structure checklist

Every production handler should follow this pattern:

```csharp
protected override async Task HandleCoreAsync(TEvent @event, CancellationToken ct)
{
    var data = @event.Data;

    // 1. Idempotency check (prevents double-processing on Polar retries)
    if (await _repo.ExistsAsync(@event.WebhookId, ct)) return;

    // 2. Extract and validate required data
    if (data.Customer?.Email is not { Length: > 0 } email)
    {
        logger.LogWarning("Event {WebhookId} has no customer email — skipping email step", @event.WebhookId);
    }

    // 3. Persist to your database
    await _repo.SaveAsync(/* mapped entity */, ct);

    // 4. Side effects (email, provisioning, external API calls)
    // — all after the database write succeeds

    // 5. Structured logging with correlation IDs
    logger.LogInformation(
        "Handled {EventType} {WebhookId} for customer {CustomerId}",
        @event.Type, @event.WebhookId, data./* relevant ID */);
}
```
