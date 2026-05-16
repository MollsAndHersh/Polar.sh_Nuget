# PolarSharp.PrepaidWallets.Polar.GraphQL

Bridge: extends PolarReportingSchema with WalletBalance / WalletHistoryEntry / FundingSource types.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.Polar.GraphQL
```

## Quickstart

```csharp
builder.Services
    .AddPolarReporting()
    .UsePostgreSqlReporting(connStr);
builder.Services
    .AddPolarPrepaidWallets()
    .UseMartenWalletEventStore(connStr);

builder.Services
    .AddPolarReportingGraphQL()
    .AddPolarWalletGraphQL();
// ...
app.MapGraphQL("/graphql/reporting");
```

## What this package does

**Polar integration bridge; stays on lift.** Extends the v1.3 `PolarSharp.Reporting.GraphQL` schema with wallet types — `WalletBalance`, `WalletHistoryEntry`, `FundingSource`, `CustomerPurchaseHistory`, `PurchaseOrderProgress` — so a single GraphQL request can return wallet data alongside catalog and reporting types. No second endpoint, no duplicate auth: the existing audience-scoped slice from `PolarSharp.Reporting.GraphQL` applies to the wallet fields too.

Per Case Study 04 "Audience-Scoped Schema Slicing", the wallet types are sliced by audience tier just like the reporting fields — Customer audience sees only their own wallet; Tenant operators see all wallets in their tenant; SaaSAdmin sees cross-tenant aggregates. The NL -> GraphQL pipeline (`PolarSharp.NaturalLanguageQuery.HotChocolate`) automatically picks up the wallet types once this bridge is registered. Per Case Study 05, this is the multi-tenant integration path — wallet core stays lift-safe; this bridge is the package that knows about Hot Chocolate + PolarSharp.Reporting.GraphQL. See `PrepaidWalletsLiftAndShift.md`.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 22.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 04: Audience-Scoped Schema Slicing](../../Case%20Studies/04-Audience-Scoped-Schema-Slicing.md)
- [Lift-And-Shift procedure for wallet packages](../../PrepaidWalletsLiftAndShift.md)
- `PolarSharp.Reporting.GraphQL` — the base schema this extends
- `PolarSharp.PrepaidWallets.Reporting` — the lift-safe reporting client backing the resolvers
- `PolarSharp.PrepaidWallets.Polar.Identity`, `.Checkout`, `.Reporting` — companion Polar bridges

## License

MIT. (c) Molls and Hersh, LLC. 2026.
