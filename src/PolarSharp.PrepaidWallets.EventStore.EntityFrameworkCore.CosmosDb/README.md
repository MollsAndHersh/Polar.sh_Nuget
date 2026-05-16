# PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.CosmosDb

Azure Cosmos DB provider for the prepaid-wallet event store (per-wallet partition key).

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.CosmosDb
```

## Quickstart

```csharp
builder.Services
    .AddPolarPrepaidWallets()
    .UseCosmosDbWalletEventStore(
        accountEndpoint: builder.Configuration["Polar:Cosmos:Endpoint"]!,
        accountKey: builder.Configuration["Polar:Cosmos:Key"]!,
        databaseName: "polar_wallets");
```

## What this package does

Hosts the wallet event-store DbContext on Azure Cosmos DB with **per-wallet partition key** (`/walletId`) so every wallet's event stream lives in its own logical partition. Append / load operations on a single wallet are single-partition (cheap in Request Units). Per-tenant isolation rides on top of partition isolation — every wallet event document carries `tenantId` and the `[AllowCrossTenant]` filter rejects cross-tenant access.

**Aggressive snapshot threshold**: the provider takes wallet snapshots every 10 events instead of the default 50 used on relational providers, because full-history replay over a long-running wallet stream is RU-expensive on Cosmos. Cosmos is **schemaless** (no EF migrations); `EnsureCreatedAsync` provisions the database + containers + indexing policies at host startup. **Lift-safe (zero PolarSharp.* deps beyond the wallet core)** — a host running Cosmos without PolarSharp.MultiTenant gets a fully working event-sourced wallet ledger from this + the core. See Case Study 02 + `PrepaidWalletsLiftAndShift.md`.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 21.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore` — the provider-agnostic core
- `PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb` — companion Cosmos tenant store
- `PolarSharp.PrepaidWallets.EventStore.Marten` — Postgres alternative

## License

MIT. (c) Molls and Hersh, LLC. 2026.
