# PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.PostgreSQL

PostgreSQL provider for the prepaid-wallet event store.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.PostgreSQL
```

## Quickstart

```csharp
builder.Services
    .AddPolarPrepaidWallets()
    .UsePostgreSqlWalletEventStore(builder.Configuration.GetConnectionString("WalletEventStore")!);
```

## What this package does

Hosts the wallet event-store DbContext (`wallet_events` + `wallet_snapshots`, optimistic concurrency on `(wallet_id, sequence_no)`) on PostgreSQL via Npgsql + EF Core — the alternative to `PolarSharp.PrepaidWallets.EventStore.Marten` for hosts who want EF Core semantics over their existing Postgres infrastructure (familiar tooling, EF migrations, no Marten projection daemon).

**Lift-safe (zero PolarSharp.* deps beyond the wallet core)** — a host running Postgres without PolarSharp.MultiTenant gets a fully working event-sourced wallet ledger from this + the core packages. Hosts also running `PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL` get defense-in-depth via Postgres RLS — the `PostgreSqlTenantSessionInterceptor` sets `app.current_tenant_id` on every connection, and the v1.3 RLS policies enforce per-tenant isolation on the wallet event tables as well. See Case Study 02 + `PrepaidWalletsLiftAndShift.md`.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 21.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets.EventStore.Marten` — Marten-native alternative on Postgres
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore` — the provider-agnostic core
- `PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL` — Postgres tenant store with RLS

## License

MIT. (c) Molls and Hersh, LLC. 2026.
