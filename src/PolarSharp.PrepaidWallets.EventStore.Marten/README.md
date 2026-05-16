# PolarSharp.PrepaidWallets.EventStore.Marten

Marten-native event store for prepaid wallets (Postgres).

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.EventStore.Marten
```

## Quickstart

```csharp
builder.Services
    .AddPolarPrepaidWallets()
    .UseMartenWalletEventStore(builder.Configuration.GetConnectionString("Wallets")!);
// Optional second arg: schemaName (defaults to "polar_marten_wallet")
```

## What this package does

Backs the wallet aggregate's event stream with Marten — Postgres-native event sourcing. Per Case Study 02 "Event-Sourced Wallet With Comprehensive Economic Modeling", Marten is the natural fit when the host already runs PostgreSQL: native event-streaming primitives (event streams, projection daemon, snapshot support) align perfectly with the wallet's aggregate model, no impedance mismatch. Wallets on other databases use the EF Core event store path instead (`PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.*`).

**Lift-safe (zero PolarSharp.* deps)** — this package depends on `PolarSharp.PrepaidWallets` + Marten directly; no `PolarSharp.MultiTenant`, no `PolarSharp.Reporting`. Hosts running on Postgres with PolarSharp's RLS providers get defense-in-depth automatically — the Marten session sets `app.current_tenant_id` so the v1.3 RLS policies enforce per-tenant isolation on Marten-driven wallet event reads as well. See `PrepaidWalletsLiftAndShift.md`.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 20.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets` — the wallet aggregate this stores
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.PostgreSQL` — EF Core alternative on Postgres
- `PolarSharp.Reporting.Marten`, `PolarSharp.AuditLog.Marten` — other Marten-backed PolarSharp surfaces

## License

MIT. (c) Molls and Hersh, LLC. 2026.
