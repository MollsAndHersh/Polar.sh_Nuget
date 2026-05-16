# PolarSharp.PrepaidWallets.Abstractions

Abstractions for the PolarSharp prepaid-wallet ledger — events, commands, queries, interfaces. Lift-shift safe (zero PolarSharp.* deps).

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.Abstractions
```

## Quickstart

```csharp
// Implement IWalletIdentityProvider against the host's actual identity infrastructure.
// In single-tenant mode that's typically a thin ASP.NET Core Identity adapter:
public sealed class AspNetCoreWalletIdentity(IHttpContextAccessor http) : IWalletIdentityProvider
{
    public Guid CurrentUserId => Guid.Parse(http.HttpContext!.User.FindFirst("sub")!.Value);
    public Guid? CurrentTenantId => null;
    public bool IsMultiTenantMode => false;
}
services.AddScoped<IWalletIdentityProvider, AspNetCoreWalletIdentity>();
```

## What this package does

Defines the contracts the wallet feature is built on — `IWalletIdentityProvider`, `WalletAudienceScope`, wallet event shapes, command/query DTOs — with **zero dependencies on any PolarSharp.* package**. This is the foundation of Case Studies 01 "Lift-And-Shift Architecture" + 02 "Event-Sourced Wallet With Comprehensive Economic Modeling" + 05 "Multi-Tenancy as Optional for Library Authors": hosts who pull this package + the core + an event-store provider get the entire wallet feature with no PolarSharp.* coupling.

**Lift-safe (zero PolarSharp.* deps)** — by design. The wallet aggregate works identically on a host using ASP.NET Core Identity directly OR PolarSharp.MultiTenant.Identity, because the only contract it depends on is `IWalletIdentityProvider`. The `WalletAudienceScope` enum collapses gracefully when `IsMultiTenantMode == false`: `HostOperator` becomes "the full-access operator", `Tenant` resolves to `HostOperator`, `Customer` stays "the end-customer". When a host outgrows their starting choice and migrates to (or away from) multi-tenancy, only the `IWalletIdentityProvider` implementation changes — the wallet code never does. See `PrepaidWalletsLiftAndShift.md` for the operational lift procedure.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 20.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 01: Lift-And-Shift Architecture](../../Case%20Studies/01-Lift-And-Shift-Architecture.md)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Case Study 05: Multi-Tenancy As Optional](../../Case%20Studies/05-Multi-Tenancy-As-Optional.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.PrepaidWallets` — the aggregate + handlers built on these abstractions

## License

MIT. (c) Molls and Hersh, LLC. 2026.
