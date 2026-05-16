# PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.CosmosDb

Azure Cosmos DB provider for the PolarSharp catalog DbContext (snapshot-mode required for hierarchical drilldown).

## Install

```sh
dotnet add package PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.CosmosDb
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseCosmosDbCatalog(
        accountEndpoint: builder.Configuration["Polar:Cosmos:Endpoint"]!,
        accountKey: builder.Configuration["Polar:Cosmos:Key"]!,
        databaseName: "polar_catalog");
```

Or via `IConfiguration` (`UseCosmosDbCatalog(configuration)` reads `PolarSharp:EcommerceStoreManagement:Sql:ConnectionString` + `:DatabaseName`).

## What this package does

Hosts the local catalog DbContext (`PolarCatalogDbContext`) on Azure Cosmos DB. Catalog entities partition on `TenantId`; per-tenant catalog reads are single-partition (cheap in Request Units); cross-tenant queries are rejected on the `[AllowCrossTenant]` filter when Cosmos is the active provider.

Cosmos is **schemaless** (no EF Core migrations) — the DbContext relies on `EnsureCreatedAsync` at host startup. Field additions are automatic; field removals require a manual data migration. **Critical**: EF Core's Cosmos provider does NOT translate `JOIN` operations, so the storefront's hierarchical drilldown (Customer -> Orders -> LineItems) MUST run against snapshot pre-aggregates from `PolarSharp.Reporting` when Cosmos is the catalog provider. The audit-log `SaveChangesInterceptor` is attached automatically when registered in DI.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 14.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 01: Lift-And-Shift Architecture](../../Case%20Studies/01-Lift-And-Shift-Architecture.md)
- `PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb` — tenant store on Cosmos
- `PolarSharp.Reporting.EntityFrameworkCore.CosmosDb` — reporting snapshots on Cosmos (required for drilldown)
- `PolarSharp.EcommerceStoreManagement.GraphQL` — GraphQL read-side over the catalog

## License

MIT. (c) Molls and Hersh, LLC. 2026.
