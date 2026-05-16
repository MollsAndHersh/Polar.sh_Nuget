# PolarSharp.PrepaidWallets.Polar.Identity

Bridge: wires PolarSharp's ICurrentUser into IWalletIdentityProvider; adapts CustomerTransactionContext for IP/UA capture in wallet event metadata.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.Polar.Identity
```

## Quickstart

```csharp
builder.Services
    .AddPolarIdentity(builder.Configuration)
    .UsePostgreSql(connStr);
builder.Services
    .AddPolarPrepaidWallets()
    .UseMartenWalletEventStore(connStr)
    .AddPolarWalletIdentityBridge();
```

## What this package does

**Polar integration bridge; stays on lift.** Wires PolarSharp.MultiTenant.Identity's `ICurrentUser` into the wallet's `IWalletIdentityProvider` abstraction so hosts running PolarSharp's multi-tenant identity get wallet operations correctly attributed to the calling user + tenant. Also adapts `CustomerTransactionContext` for IP / UA capture in wallet event metadata, so wallet events carry the same provenance trail as Polar webhook ingestion + EF SaveChanges interceptor records.

Per Case Study 05 "Multi-Tenancy as Optional for Library Authors", this bridge is the **multi-tenant integration path**. Single-tenant hosts use the (forthcoming) `PolarSharp.PrepaidWallets.AspNetCore.Identity` package instead — same `IWalletIdentityProvider` contract, different identity backend. Pulling THIS bridge is what attaches the wallet feature to the PolarSharp Identity stack; if a host lifts off PolarSharp.MultiTenant later, they swap in the AspNetCore.Identity bridge without changing any wallet code. See `PrepaidWalletsLiftAndShift.md` for the lift procedure.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 22.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 05: Multi-Tenancy As Optional](../../Case%20Studies/05-Multi-Tenancy-As-Optional.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets.Abstractions` — defines `IWalletIdentityProvider`
- `PolarSharp.MultiTenant.Identity` — the ICurrentUser implementation the bridge wires in
- `PolarSharp.PrepaidWallets.Polar.Checkout`, `.Reporting`, `.GraphQL` — companion Polar bridges

## License

MIT. (c) Molls and Hersh, LLC. 2026.
