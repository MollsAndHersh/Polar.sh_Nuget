# PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.MariaDb

MariaDB / MySQL provider for the PolarSharp catalog DbContext.

## Install

```sh
dotnet add package PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.MariaDb
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseMariaDbCatalog("Server=mariadb.internal;Database=polar_catalog;User Id=app;Password=...");
```

Or with configuration:

```csharp
builder.Services.UseMariaDbCatalog(builder.Configuration);
// reads PolarSharp:EcommerceStoreManagement:Sql:ConnectionString or :ConnectionStringName
```

## What this package does

Hosts the local catalog DbContext (`PolarCatalogDbContext`) — products, variants, categories, departments, discounts, checkout links, business profile — on MariaDB / MySQL. The local catalog is the read-side mirror of Polar's authoritative catalog, kept fresh by webhooks and the publisher orchestrator.

Per-tenant isolation on this provider is **app-layer (EF Core global query filter) only** — MariaDB / MySQL lack Postgres-style `ROW LEVEL SECURITY`, so a code path that bypasses the DbContext is not protected by a database policy. Same posture as the SQLite catalog provider. Hosts wanting defense-in-depth should pick Postgres or SQL Server instead. The audit-log `SaveChangesInterceptor` (from V20-013) is attached automatically when it is registered in DI.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 13.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 01: Lift-And-Shift Architecture](../../Case%20Studies/01-Lift-And-Shift-Architecture.md)
- `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL` — defense-in-depth via RLS
- `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer` — defense-in-depth via RLS
- `PolarSharp.EcommerceStoreManagement.GraphQL` — GraphQL read-side over the catalog

## License

MIT. (c) Molls and Hersh, LLC. 2026.
