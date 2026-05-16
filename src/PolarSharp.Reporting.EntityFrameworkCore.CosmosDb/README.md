# PolarSharp.Reporting.EntityFrameworkCore.CosmosDb

Azure Cosmos DB provider for the PolarSharp reporting snapshot DbContext.

## Install

```sh
dotnet add package PolarSharp.Reporting.EntityFrameworkCore.CosmosDb
```

## Quickstart

```csharp
builder.Services
    .AddPolarReporting()
    .UseCosmosDbReporting(
        accountEndpoint: builder.Configuration["Polar:Cosmos:Endpoint"]!,
        accountKey: builder.Configuration["Polar:Cosmos:Key"]!,
        databaseName: "polar_reporting");
```

A connection-string overload (`UseCosmosDbReporting(connectionString, databaseName)`) is also available.

## What this package does

Hosts the reporting-snapshot DbContext (`PolarReportingDbContext`) on Azure Cosmos DB, plus an `IPolarReportingClient` implementation. Snapshot entities partition on `TenantId`; the snapshot writer goes to single partitions (efficient); cross-tenant aggregations used by SaaSAdmin reports are rejected on Cosmos because they require cross-partition queries with high RU cost — SaaSAdmin cross-tenant reporting needs an alternate provider (Postgres / SqlServer) when those reports matter to the host.

**Snapshot mode is mandatory on Cosmos for hierarchical drilldown**. EF Core's Cosmos provider does NOT translate JOIN operations, so Customer -> Orders -> LineItems drilldown reads pre-aggregated parent records (customer with `order_count` + `lifetime_value`) plus per-customer single-partition queries for the order list. The package also includes a design-time `IDesignTimeDbContextFactory` so `dotnet ef migrations add` works without a running tenant.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 14.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 05: Multi-Tenancy As Optional](../../Case%20Studies/05-Multi-Tenancy-As-Optional.md)
- `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.CosmosDb` — catalog on Cosmos
- `PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL` — RLS-backed alternative for SaaSAdmin cross-tenant reports
- `PolarSharp.Reporting.GraphQL` — GraphQL read-side over reporting snapshots

## License

MIT. (c) Molls and Hersh, LLC. 2026.
