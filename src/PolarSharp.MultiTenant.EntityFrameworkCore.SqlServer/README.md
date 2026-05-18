# PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer

SQL Server provider for PolarSharp's EF Core-backed tenant store. Adds defense-in-depth cross-tenant isolation via SQL Server Row-Level Security policies on top of EF Core query filters.

## Install

```bash
dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseSqlServer("Server=...;Database=PolarTenants;Trusted_Connection=true;");
```

Tenant data is stored in a single shared database; rows are filtered per-tenant via EF Core's global query filter AND a SQL Server `SECURITY POLICY` with `tenant_filter` table-valued function predicate. The `AppMasterAdmin` session-context bypass is set only on routes explicitly annotated `[AllowCrossTenant]`.

See `docs/articles/tenant-isolation.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/) for full RLS setup, session-var lifecycle, and bypass auditing.

## Single-tenant -> multi-tenant upgrade

Hosts that started as single-tenant deployments can opt into the automated upgrade migrator:

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseSqlServer(connStr);

builder.Services.AddPolarSingleTenantUpgrade(builder.Configuration);
```

On the next boot, the SQL Server migrator:

1. Upserts the configured default tenant into `polar_tenants`.
2. Iterates every entity in the model that implements `ITenantOwned` and issues a bulk `UPDATE … SET TenantId = @default WHERE TenantId IS NULL OR TenantId = ''` against each — no rows are loaded into memory.
3. Records a row in `polar_upgrade_history` so subsequent boots short-circuit.
4. Runs the whole backfill in a single transaction so a mid-flight failure rolls back cleanly.

**RLS interaction.** The shipped `EnableRowLevelSecurity` policy filters every tenant-owned table by `SESSION_CONTEXT('tenant_id')`. The migrator temporarily sets `SESSION_CONTEXT('is_app_master_admin') = 1` (the same bypass used by `[AllowCrossTenant]` routes) for the duration of the backfill so the `UPDATE` statements can touch rows whose `TenantId` is currently NULL. The bypass is reset in a `try / finally`, so a crash mid-upgrade does not leak the flag back into the connection pool.

## Migration from `appsettings.json`

```csharp
.UseSqlServer(connStr, seedFromAppSettings: true);
```

On first startup, copies all entries from `PolarSharp:MultiTenant:Tenants` into the database. Logs the seeded count. After seed completes, remove the `Tenants` array from `appsettings.json`.

## License

MIT.
