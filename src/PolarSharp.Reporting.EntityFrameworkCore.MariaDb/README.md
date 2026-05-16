# PolarSharp.Reporting.EntityFrameworkCore.MariaDb

MariaDB / MySQL provider for the PolarSharp reporting snapshot DbContext.

## Install

```sh
dotnet add package PolarSharp.Reporting.EntityFrameworkCore.MariaDb
```

## Quickstart

```csharp
builder.Services
    .AddPolarReporting()
    .UseMariaDbReporting("Server=mariadb.internal;Database=polar_reporting;User Id=app;Password=...");
```

## What this package does

Hosts the reporting-snapshot DbContext (`PolarReportingDbContext`) on MariaDB / MySQL, plus an `IPolarReportingClient` implementation that reads pre-aggregated snapshot rows for transactions, subscriptions, orders, customers, and entitlements. The snapshot service writes per-tenant aggregates so the dashboard's KPI widgets and drilldown queries are single-table reads instead of cross-joins against the operational catalog.

Per-tenant isolation is **app-layer query-filter only** on this provider — MariaDB / MySQL lack Postgres `ROW LEVEL SECURITY`, so the EF Core global query filter is the single enforcement layer. Same posture as the SQLite reporting provider. The package also includes a design-time `IDesignTimeDbContextFactory` so `dotnet ef migrations add` works without a running tenant.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 13.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 05: Multi-Tenancy As Optional](../../Case%20Studies/05-Multi-Tenancy-As-Optional.md)
- `PolarSharp.Reporting.Marten` — Marten event-sourced alternative when on Postgres
- `PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL` — defense-in-depth via RLS
- `PolarSharp.Reporting.GraphQL` — GraphQL read-side over reporting snapshots

## License

MIT. (c) Molls and Hersh, LLC. 2026.
