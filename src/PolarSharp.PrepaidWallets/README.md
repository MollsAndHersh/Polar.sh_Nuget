# PolarSharp.PrepaidWallets

Event-sourced Wallet aggregate + MediatR command/query handlers + projections + snapshot strategy.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets
```

## Quickstart

```csharp
builder.Services
    .AddPolarPrepaidWallets()
    .UseSqlServerWalletEventStore(builder.Configuration.GetConnectionString("WalletEventStore")!);
// Then register an IWalletIdentityProvider (or AddPolarWalletIdentityBridge() if running on PolarSharp.MultiTenant.Identity).
```

## What this package does

The core wallet feature — event-sourced `Wallet` aggregate, MediatR command/query handlers, behaviors (idempotency / validation / logging / transaction), projections to balance + history read models, and the snapshot strategy. Per Case Study 02 "Event-Sourced Wallet With Comprehensive Economic Modeling", the wallet treats every credit / debit / hold / refund as an immutable event; the current balance is a projection; snapshots compress replay cost on hot wallets.

**Lift-safe (zero PolarSharp.* deps)** — the core depends only on `PolarSharp.PrepaidWallets.Abstractions`. A host using ASP.NET Core Identity directly gets the entire wallet feature by pulling this + Abstractions + a storage provider (Marten on Postgres OR EF Core with any of SQL Server / SQLite / Postgres / MariaDB / Cosmos). Hosts on PolarSharp.MultiTenant.Identity get the Polar bridges as separate packages (`PolarSharp.PrepaidWallets.Polar.*`), keeping the lift-safe boundary clean. See `PrepaidWalletsLiftAndShift.md` for the operational lift procedure when moving onto / off of multi-tenancy.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 20.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 01: Lift-And-Shift Architecture](../../Case%20Studies/01-Lift-And-Shift-Architecture.md)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets.Abstractions` — the lift-safe contracts
- `PolarSharp.PrepaidWallets.EventStore.*` — storage providers (Marten + 5 EF Core)

## License

MIT. (c) Molls and Hersh, LLC. 2026.
