# PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore

Provider-agnostic EF Core event store for prepaid wallets (wallet_events + wallet_snapshots tables).

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore
```

Plus one of the provider packages (`.SqlServer`, `.Sqlite`, `.PostgreSQL`, `.MariaDb`, `.CosmosDb`).

## Quickstart

```csharp
// The provider-agnostic core registers the DbContext + IWalletEventStore;
// the provider package (e.g. UseSqlServerWalletEventStore(...)) plugs in the database.
builder.Services
    .AddPolarPrepaidWallets()
    .UseSqlServerWalletEventStore(builder.Configuration.GetConnectionString("Wallets")!);
```

## What this package does

The shared EF Core event-store core — defines `IWalletEventStore`, the `wallet_events` + `wallet_snapshots` schema, optimistic concurrency via unique `(wallet_id, sequence_no)` index, and the load/append APIs the wallet aggregate uses. Provider packages plug in the actual database (SQL Server / SQLite / PostgreSQL / MariaDB / Cosmos).

The schema:

```
wallet_events     id, wallet_id, sequence_no, event_type, event_payload_json,
                  idempotency_key, occurred_at, actor_user_id, source_ip_hash
wallet_snapshots  wallet_id, sequence_no, balance, frozen, closed,
                  snapshot_payload_json, taken_at
```

Optimistic concurrency: second writer on a stale read fails with `WalletConcurrencyConflictException`; the MediatR retry behavior recovers. **Lift-safe (zero PolarSharp.* deps)** — this package depends only on `PolarSharp.PrepaidWallets` + EF Core; hosts can lift the wallet ledger to a non-PolarSharp host by keeping the same schema. See Case Study 02 and `PrepaidWalletsLiftAndShift.md`.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 20.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.SqlServer` — SQL Server provider
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.PostgreSQL` — Postgres provider
- `PolarSharp.PrepaidWallets.EventStore.Marten` — Marten alternative on Postgres

## License

MIT. (c) Molls and Hersh, LLC. 2026.
