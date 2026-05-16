# PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb

MariaDB / MySQL provider for PolarSharp.MultiTenant.EntityFrameworkCore — tenant store with EF Core query filter-based isolation (no DB-layer RLS available on MariaDB).

## Install

```sh
dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseMariaDb("Server=mariadb.internal;Database=polar_tenants;User Id=polar;Password=...");
```

## What this package does

Stores PolarSharp's `PolarTenantInfo` registry in a MariaDB / MySQL database via Oracle's `MySql.EntityFrameworkCore` provider. The provider works against both MariaDB >= 10.5 and MySQL >= 8.0.

Tenant isolation on this provider is **app-layer (EF Core global query filter) only** — MariaDB / MySQL do not expose Postgres-style `ROW LEVEL SECURITY`, so a bug or misconfiguration that bypasses the DbContext (e.g. raw `IDbConnection` queries) will NOT be caught by a database policy. That is the Postgres / SQL Server posture, not the MariaDB posture. The same single-layer posture applies to the SQLite provider in this family. Hosts that require defense-in-depth at the DB layer should choose Postgres or SQL Server. See Case Study 05 "Multi-Tenancy As Optional" for how the multi-tenant pieces opt in.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 13.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 05: Multi-Tenancy As Optional](../../Case%20Studies/05-Multi-Tenancy-As-Optional.md)
- `PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL` — stronger isolation posture (RLS)
- `PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer` — stronger isolation posture (RLS)
- `PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite` — same single-layer query-filter posture

## License

MIT. (c) Molls and Hersh, LLC. 2026.
