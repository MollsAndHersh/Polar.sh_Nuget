# PolarSharp.PrepaidWallets.Polar.Checkout

Polar.sh integration bridge for the wallet. Covers two distinct roles in one package: (1) catalog-checkout integration with wallet-only / hybrid / Polar-only modes, refund-as-credit, and subscription billing-cycle debits; (2) **the `PolarFundingProcessor` implementation that lets tenants prepay for platform usage by funding their prepaid wallets via Polar.sh — the primary on-ramp for the SaaS tenant prepayment flow**.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.Polar.Checkout
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UsePostgreSql(connStr);
builder.Services
    .AddPolarPrepaidWallets()
    .UseMartenWalletEventStore(connStr)
    .AddPolarWalletCheckoutBridge();
// Per-tenant config (PolarSharp:Wallet:CheckoutMode in appsettings) picks
// WalletOnly / Hybrid / PolarOnly for the catalog-checkout side.
// Per-tenant config (PolarSharp:Wallet:FundingProcessor) picks the funding
// processor — Polar.Checkout registers itself as the "Polar" option.
```

## What this package does

**Polar integration bridge; stays with PolarSharp on lift.** Two distinct integration roles bundled in one package:

### Role 1: Catalog checkout integration (three configurable modes per tenant)

- **WalletOnly** — purchases debit the wallet; if the wallet can't cover, the checkout fails before reaching Polar.
- **Hybrid** — wallet pays what it can; remainder routes to Polar's normal payment flow.
- **PolarOnly** — checkout bypasses the wallet entirely (per-tenant kill switch).

Also handles **refund-as-credit** (saves the Polar refund fee by crediting the wallet instead of returning to source) and **subscription debit** on recurring billing-cycle ticks. Order / PO / product linkage is snapshotted at debit time (per v1.3 plan amendment 1) so reports always show what was bought even if the catalog later changes.

### Role 2: Polar.sh as a wallet funding processor (the primary on-ramp for SaaS tenant prepayment)

This package contains the `PolarFundingProcessor` implementation of `IWalletFundingProcessor` — the on-ramp that lets tenants prepay for platform usage by funding their own prepaid wallets via Polar.sh. End-to-end flow:

1. Tenant operator clicks "Top up platform balance" in the SaaS's admin UI.
2. Host calls `IWalletFundingProcessor.InitiatePaymentAsync(walletId, amount)`.
3. `PolarFundingProcessor` creates a Polar Order with the tenant's organization as the buyer + a wallet-funding metadata tag.
4. Customer (= the tenant in this context) is redirected to Polar's hosted checkout; pays with their chosen payment instrument.
5. Polar fires the `order.paid` webhook → PolarSharp.Webhooks → Polar.Checkout handler.
6. Handler translates Polar order details into a `FundWalletCommand` carrying the full fee breakdown.
7. Wallet aggregate appends a `WalletFunded` event with all economic fields snapshotted (customer charged amount, processor fee, SaaS profit, tenant absorbed, tenant net, tokens credited, funding-terms snapshot).
8. Wallet projection updates; tenant sees the new balance in real time via SignalR push.

**Why this is the primary on-ramp**: the Polar.sh funding path is the only one of the three shipped processors (Stripe, PayPal, Polar.sh) that is PolarSharp-coupled, and it is the on-ramp for the entire Option D `TenantPrefundedWallet` settlement mode. When the SaaS chooses Option D, every tenant prepays for platform usage via this exact flow; those payments credit the tenant's own prepaid wallet; SaaS fees then debit that wallet in real time as the tenant operates and as the tenant's customers transact. The recursion — the wallet system used twice, once at the customer-to-tenant level and once at the tenant-to-SaaS level — is what makes Option D possible, and the `PolarFundingProcessor` in this package is the on-ramp for both levels.

For non-Polar funding (hosts that want a fully-lift-safe stack, or hosts on settlement mode A `StripeConnect`), install `PolarSharp.PrepaidWallets.Funding.Stripe` or `PolarSharp.PrepaidWallets.Funding.PayPal` instead. Those packages provide alternative `IWalletFundingProcessor` implementations and have no PolarSharp.* dependencies.

Per Case Study 05, this is the multi-tenant integration path — the wallet core itself stays lift-safe; this bridge is the package that knows about `PolarSharp.EcommerceStoreManagement` and `PolarSharp.MultiTenant.Identity`.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 22.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md) — especially the "Funding flow: Polar.sh as the primary on-ramp" section
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md) — explains how this bridge's funding role survives a future lift of the wallet core
- `PolarSharp.PrepaidWallets` — the wallet aggregate
- `PolarSharp.EcommerceStoreManagement` — the catalog checkout pipeline being bridged
- `PolarSharp.PrepaidWallets.Funding.Stripe` + `PolarSharp.PrepaidWallets.Funding.PayPal` — the two lift-safe alternative funding processors
- `PolarSharp.PrepaidWallets.Polar.Identity`, `.Reporting`, `.GraphQL`, `.SaaSInvoicing` — companion Polar bridges

## License

MIT. (c) Molls and Hersh, LLC. 2026.
