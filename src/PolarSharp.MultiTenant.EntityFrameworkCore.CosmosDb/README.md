# PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb

Azure Cosmos DB provider for PolarSharp.MultiTenant.EntityFrameworkCore — tenant store with per-tenant partition-key isolation.

## Install

```sh
dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseCosmosDb(
        accountEndpoint: builder.Configuration["Polar:Cosmos:Endpoint"]!,
        accountKey: builder.Configuration["Polar:Cosmos:Key"]!,
        databaseName: "polar_tenants");
```

A connection-string overload (`UseCosmosDb(connectionString, databaseName)`) is also available.

## What this package does

Stores PolarSharp's tenant registry in Azure Cosmos DB using EF Core's Cosmos provider. Where the SQL providers use row-level security policies and the SQLite provider uses per-tenant `.db` files, the Cosmos provider uses Cosmos's native **logical partition key** as the isolation primitive — every tenant-owned entity is configured with `HasPartitionKey(e => e.TenantId)`. Per-tenant queries are single-partition (cheap in Request Units); cross-tenant queries are inherently cross-partition (RU-expensive) and explicitly rejected by the `[AllowCrossTenant]` filter when Cosmos is the active provider.

Cosmos is **schemaless** — there are no EF Core migrations. The provider relies on `Database.EnsureCreatedAsync()` at host startup to create the database, containers, and indexing policies. Adding fields is automatic (documents are schemaless); removing fields requires a manual data migration outside EF Core. Hierarchical drilldown queries (customer -> orders -> line items) do NOT translate to JOINs on Cosmos, so Cosmos hosts must enable PolarSharp.Reporting snapshot mode with pre-aggregation. See Case Study 05 "Multi-Tenancy As Optional".

## Single-tenant -> multi-tenant upgrade

Hosts that started as single-tenant deployments can opt into the automated upgrade migrator:

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseCosmosDb(endpoint, key, "polar_tenants");

builder.Services.AddPolarSingleTenantUpgrade(builder.Configuration);
```

On the next boot, the Cosmos migrator:

1. Upserts the configured default tenant into the tenant registry.
2. Iterates every entity in the model that implements `ITenantOwned`, reads each item whose `TenantId` is currently null or empty, sets the property to the default tenant id, and re-saves it. Cosmos issues a `ReplaceItem` call under the hood — partition-key changes are not in-place updates.
3. Records a document in `polar_upgrade_history` so subsequent boots short-circuit.

**RU budget gate.** Cosmos charges RUs for every read, write, and replace, so a careless upgrade can show up on the bill. The migrator performs a dry-run count pass and refuses to run when the estimated cost exceeds the configured threshold:

```jsonc
{
  "PolarSharp": {
    "MultiTenant": {
      "SingleTenantUpgrade": {
        "Cosmos": {
          "AbortIfEstimatedRuCostExceeds": 10000,
          "AcknowledgeCosmosRuCost": false
        }
      }
    }
  }
}
```

Set `AcknowledgeCosmosRuCost` to `true` (or pass `--force` on the upcoming CLI) to proceed past the budget gate. The estimate is a deliberately rough `itemCount × 10 RUs/replace` — operators with unusually large documents should raise `AbortIfEstimatedRuCostExceeds` to match reality.

**No EF migrations.** Cosmos has no schema-migration concept, so there is no `AddUpgradeHistoryTable` migration file. The package ships a `CosmosUpgradeHistoryContainerProvisioner` hosted service that calls `EnsureCreatedAsync` at host startup to create the `polar_upgrade_history` container the first time it is needed.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 14.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 05: Multi-Tenancy As Optional](../../Case%20Studies/05-Multi-Tenancy-As-Optional.md)
- `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.CosmosDb` — catalog on Cosmos
- `PolarSharp.Reporting.EntityFrameworkCore.CosmosDb` — reporting snapshots on Cosmos
- `PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL` — RLS-based defense-in-depth

## License

MIT. (c) Molls and Hersh, LLC. 2026.
