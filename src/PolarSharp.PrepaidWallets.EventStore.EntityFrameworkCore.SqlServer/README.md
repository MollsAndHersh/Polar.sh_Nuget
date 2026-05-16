# PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.SqlServer

SQL Server provider for the prepaid-wallet event store.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.SqlServer
```

## Quickstart

```csharp
builder.Services
    .AddPolarPrepaidWallets()
    .UseSqlServerWalletEventStore(builder.Configuration.GetConnectionString("WalletEventStore")!);
```

## What this package does

Hosts the wallet event-store DbContext (`wallet_events` + `wallet_snapshots` tables, optimistic concurrency on `(wallet_id, sequence_no)`) on SQL Server. **Lift-safe (zero PolarSharp.* deps beyond the wallet core)** — hosts running SQL Server on their own (no PolarSharp.MultiTenant) get a fully working event-sourced wallet ledger from this + the core packages. Hosts also running `PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer` get defense-in-depth via SQL Server's session-context-backed RLS — the same `app.current_tenant_id` mechanism that protects the tenant store and Identity tables protects the wallet event stream too.

See Case Study 02 "Event-Sourced Wallet With Comprehensive Economic Modeling" for the aggregate / projection design and `PrepaidWalletsLiftAndShift.md` for the operational lift procedure.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 21.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets` — the wallet aggregate
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore` — the provider-agnostic core
- `PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer` — SQL Server tenant store with RLS

## License

MIT. (c) Molls and Hersh, LLC. 2026.
