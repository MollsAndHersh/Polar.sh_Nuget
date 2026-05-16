# PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Sqlite

SQLite provider for the prepaid-wallet event store.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Sqlite
```

## Quickstart

```csharp
builder.Services
    .AddPolarPrepaidWallets()
    .UseSqliteWalletEventStore("/var/polar/wallets");
// First arg is a filesystem directory; one wallet event-store .db file per tenant.
```

## What this package does

Hosts the wallet event-store DbContext (`wallet_events` + `wallet_snapshots`) on SQLite using **per-tenant `.db` file** isolation — matches the v1.2 SQLite catalog provider's per-tenant-file approach. Each tenant gets its own physical SQLite file; there is no shared database, so cross-tenant queries are structurally impossible. Strong isolation posture at the file-system level; ideal for compliance scenarios where tenant data must be physically separated and easy to backup, restore, or hand off independently.

**Lift-safe (zero PolarSharp.* deps beyond the wallet core)** — single-tenant hosts get a fully working event-sourced wallet ledger without pulling any multi-tenant infrastructure. See Case Study 02 "Event-Sourced Wallet With Comprehensive Economic Modeling" for the aggregate / projection design and `PrepaidWalletsLiftAndShift.md` for the operational lift procedure.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 21.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets` — the wallet aggregate
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore` — the provider-agnostic core
- `PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite` — companion SQLite tenant store

## License

MIT. (c) Molls and Hersh, LLC. 2026.
