# PolarSharp.PrepaidWallets.Polar.Checkout

Bridge: wallet-only / hybrid / disabled checkout modes; refund-as-credit; subscription debit.

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
// WalletOnly / Hybrid / PolarOnly.
```

## What this package does

**Polar integration bridge; stays on lift.** Wires the wallet into the catalog's checkout flow with three configurable modes per tenant:

- **WalletOnly** — purchases debit the wallet; if the wallet can't cover, the checkout fails before reaching Polar.
- **Hybrid** — wallet pays what it can; remainder routes to Polar's normal payment flow.
- **PolarOnly** — checkout bypasses the wallet entirely (per-tenant kill switch).

Also handles **refund-as-credit** (saves the Polar refund fee by crediting the wallet instead of returning to source) and **subscription debit** on recurring billing-cycle ticks. Order / PO / product linkage is snapshotted at debit time (per v1.3 plan amendment 1) so reports always show what was bought even if the catalog changes. Per Case Study 05, this is the multi-tenant integration path — the wallet core itself stays lift-safe; this bridge is the package that knows about PolarSharp.EcommerceStoreManagement. See `PrepaidWalletsLiftAndShift.md`.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 22.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets` — the wallet aggregate
- `PolarSharp.EcommerceStoreManagement` — the catalog checkout pipeline being bridged
- `PolarSharp.PrepaidWallets.Polar.Identity`, `.Reporting`, `.GraphQL` — companion Polar bridges

## License

MIT. (c) Molls and Hersh, LLC. 2026.
