# PolarSharp.PrepaidWallets.Polar.Reporting

Bridge: surfaces wallet sections inside PolarSharp.Reporting + audit log integration.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.Polar.Reporting
```

## Quickstart

```csharp
builder.Services
    .AddPolarReporting()
    .UsePostgreSqlReporting(connStr);
builder.Services
    .AddPolarPrepaidWallets()
    .UseMartenWalletEventStore(connStr)
    .AddPolarWalletReportingBridge();
```

## What this package does

**Polar integration bridge; stays on lift.** Two things happen when this bridge is registered:

1. Wallet sections — current balance, transaction history, funding sources — are surfaced inside the existing `IPolarReportingClient`, so dashboards that already render catalog + subscription + order reports get wallet widgets without re-plumbing.
2. The wallet's `SaveChangesInterceptor` writes a corresponding `AdminAuditLogEntry` alongside every wallet event — same audit trail as catalog mutations, identity changes, and webhook ingestion.

Reports added in Phase 22.x (per v1.3 plan amendment 1): `CustomerPurchaseHistoryReport`, `PurchaseOrderProgressReport`, `RefundReconciliationReport`. Each is generated from the wallet's projections + the catalog's `PurchaseOrder` aggregate; nothing flows back from the wallet to the catalog. Per Case Study 05, this bridge is the integration path — the wallet core stays lift-safe; the bridge is the layer that knows about PolarSharp.Reporting. See `PrepaidWalletsLiftAndShift.md`.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 22.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets.Reporting` — the lift-safe reporting client this bridges into
- `PolarSharp.Reporting` — the umbrella reporting feature being extended
- `PolarSharp.PrepaidWallets.Polar.Identity`, `.Checkout`, `.GraphQL` — companion Polar bridges

## License

MIT. (c) Molls and Hersh, LLC. 2026.
