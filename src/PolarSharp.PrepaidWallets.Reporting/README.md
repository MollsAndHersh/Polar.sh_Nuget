# PolarSharp.PrepaidWallets.Reporting

Read-side reporting for prepaid wallets — SaaS / tenant / customer audiences.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.Reporting
```

## Quickstart

```csharp
// Inject IWalletReportingClient after registering the wallet feature + an event store provider:
public sealed class WalletDashboardPage(IWalletReportingClient reporting)
{
    public async Task<long> CurrentBalanceAsync(Guid customerId, CancellationToken ct)
        => await reporting.GetBalanceAsync(customerId, ct);
}
```

## What this package does

Read-side reporting API for the prepaid wallet — hosts query wallet balances, history, funding sources, and audience-scoped aggregates through `IWalletReportingClient`. The implementation reads from wallet projections built off the event stream; it does NOT replay events on every query.

**Lift-safe (zero PolarSharp.* deps)** — the package depends only on `PolarSharp.PrepaidWallets.Abstractions` + a wallet event-store provider. Hosts running ASP.NET Core Identity directly (no PolarSharp.MultiTenant) get the same reporting API; PolarSharp.MultiTenant hosts get the same API plus the audit-log integration + reporting-section bridge from `PolarSharp.PrepaidWallets.Polar.Reporting`. The shapes that land in Phase 22.x — `CustomerPurchaseHistoryReport`, `PurchaseOrderProgressReport`, `RefundReconciliationReport` (per v1.3 plan amendment 1) — are also lift-safe. See `PrepaidWalletsLiftAndShift.md`.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 22.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets` — the wallet aggregate
- `PolarSharp.PrepaidWallets.Polar.Reporting` — Polar-integration bridge (audit log + reporting section)

## License

MIT. (c) Molls and Hersh, LLC. 2026.
