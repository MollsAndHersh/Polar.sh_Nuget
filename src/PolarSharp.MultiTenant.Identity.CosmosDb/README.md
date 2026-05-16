# PolarSharp.MultiTenant.Identity.CosmosDb

Azure Cosmos DB provider for PolarSharp.MultiTenant.Identity — Identity tables with per-tenant partition-key isolation.

## Install

```sh
dotnet add package PolarSharp.MultiTenant.Identity.CosmosDb
```

## Quickstart

```csharp
builder.Services
    .AddPolarIdentity(builder.Configuration)
    .UseCosmosDb(
        accountEndpoint: builder.Configuration["Polar:Cosmos:Endpoint"]!,
        accountKey: builder.Configuration["Polar:Cosmos:Key"]!,
        databaseName: "polar_identity")
    .AddCoreIdentityServices();
```

Connection-string and `IConfiguration` overloads are also available (`UseCosmosDb(connectionString, databaseName)` and `UseCosmosDb(configuration)`).

## What this package does

Hosts `PolarUserDbContext` (PolarSharp's ASP.NET Core Identity tables — users, roles, claims, memberships) on Azure Cosmos DB. Identity tables all partition on the tenant id; per-tenant queries are single-partition reads (cheap in Request Units); cross-tenant queries are cross-partition (RU-expensive) and rejected on the `[AllowCrossTenant]` filter when Cosmos is the active provider.

Cosmos has no schema-migration concept — the Identity DbContext relies on `EnsureCreatedAsync` at host startup to provision the database + containers. The provider is registered with health-check tags `polar-sql`, `polar-identity`, `polar-cosmosdb`. See Case Study 05 "Multi-Tenancy As Optional" for how Identity collapses gracefully in single-tenant deployments.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 14.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 05: Multi-Tenancy As Optional](../../Case%20Studies/05-Multi-Tenancy-As-Optional.md)
- `PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb` — tenant store on Cosmos
- `PolarSharp.MultiTenant.Identity.PostgreSQL` — defense-in-depth via RLS
- `PolarSharp.MultiTenant.Identity.SqlServer` — defense-in-depth via RLS

## License

MIT. (c) Molls and Hersh, LLC. 2026.
