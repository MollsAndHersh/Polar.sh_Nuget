# PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.MariaDb

MariaDB / MySQL provider for the prepaid-wallet event store.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.MariaDb
```

## Quickstart

```csharp
builder.Services
    .AddPolarPrepaidWallets()
    .UseMariaDbWalletEventStore("Server=mariadb.internal;Database=polar_wallets;User Id=app;Password=...");
```

## What this package does

Hosts the wallet event-store DbContext (`wallet_events` + `wallet_snapshots`) on MariaDB / MySQL via Oracle's `MySql.EntityFrameworkCore` provider (works against MariaDB >= 10.5 and MySQL >= 8.0). Per-tenant isolation is **app-layer query-filter only** on this provider — MariaDB / MySQL lack Postgres-style `ROW LEVEL SECURITY`, so the EF Core global query filter is the single enforcement layer. Same posture as the v1.3 MariaDB tenant store and the SQLite wallet event store on the file-isolation side.

**Lift-safe (zero PolarSharp.* deps beyond the wallet core)** — hosts running on MariaDB or MySQL get a fully working event-sourced wallet ledger from this + the core packages. Hosts that require defense-in-depth at the DB layer should choose Postgres or SQL Server instead. See Case Study 02 + `PrepaidWalletsLiftAndShift.md`.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 21.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.SqlServer` — defense-in-depth via RLS
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.PostgreSQL` — defense-in-depth via RLS
- `PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb` — companion MariaDB tenant store

## License

MIT. (c) Molls and Hersh, LLC. 2026.
