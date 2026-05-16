# PolarSharp.MultiTenant.Identity.MariaDb

MariaDB / MySQL provider for PolarSharp.MultiTenant.Identity — Identity tables with EF Core query filter-based isolation.

## Install

```sh
dotnet add package PolarSharp.MultiTenant.Identity.MariaDb
```

## Quickstart

```csharp
builder.Services
    .AddPolarIdentity(builder.Configuration)
    .UseMariaDb("Server=mariadb.internal;Database=polar_identity;User Id=app;Password=...")
    .AddCoreIdentityServices();
```

## What this package does

Hosts `PolarUserDbContext` (PolarSharp's ASP.NET Core Identity tables — users, roles, claims, memberships) on MariaDB / MySQL. Three deployment shapes are supported: explicit connection string, `IConfiguration`-driven (reads `PolarSharp:Identity:Sql`), or share the host's existing DbContext via `UseHostDbContext<TContext>()` in the base package.

Per-tenant isolation on this provider is **app-layer query-filter only**, matching the family's tenant-store posture: MariaDB / MySQL lack Postgres-style `ROW LEVEL SECURITY`, so the DbContext's global query filter is the only enforcement layer. Same posture as the SQLite Identity provider. Hosts requiring DB-layer defense-in-depth should pick Postgres or SQL Server instead. See Case Study 05 "Multi-Tenancy As Optional" for how Identity collapses gracefully in single-tenant deployments.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 13.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 05: Multi-Tenancy As Optional](../../Case%20Studies/05-Multi-Tenancy-As-Optional.md)
- `PolarSharp.MultiTenant.Identity.PostgreSQL` — defense-in-depth via RLS
- `PolarSharp.MultiTenant.Identity.SqlServer` — defense-in-depth via RLS
- `PolarSharp.AuditLog.Marten` — Marten event-sourced alternative for the audit log table

## License

MIT. (c) Molls and Hersh, LLC. 2026.
