# PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL

PostgreSQL provider for PolarSharp's EF Core-backed tenant store. Adds defense-in-depth cross-tenant isolation via PostgreSQL Row-Level Security policies on top of EF Core query filters.

## Install

```bash
dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UsePostgreSql("Host=...;Database=polar_tenants;Username=...;Password=...");
```

Tenant data is stored in a single shared database; rows are filtered per-tenant via EF Core's global query filter AND `FORCE ROW LEVEL SECURITY` + `CREATE POLICY tenant_isolation` predicates. The `AppMasterAdmin` session-var (`app.is_app_master_admin`) bypass is set only on routes explicitly annotated `[AllowCrossTenant]`.

See `docs/articles/tenant-isolation.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/) for full RLS setup, `set_config()` lifecycle, and bypass auditing.

## Single-tenant -> multi-tenant upgrade

Hosts that started as single-tenant deployments can opt into the automated upgrade migrator:

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UsePostgreSql(connStr);

builder.Services.AddPolarSingleTenantUpgrade(builder.Configuration);
```

On the next boot, the PostgreSQL migrator:

1. Upserts the configured default tenant into `polar_tenants`.
2. Iterates every entity in the model that implements `ITenantOwned` and issues a bulk `UPDATE … SET "TenantId" = $1 WHERE "TenantId" IS NULL OR "TenantId" = ''` against each — no rows are loaded into memory.
3. Records a row in `polar_upgrade_history` so subsequent boots short-circuit.
4. Runs the whole backfill in a single transaction so a mid-flight failure rolls back cleanly.

**RLS interaction.** The shipped `tenant_isolation` policy filters every tenant-owned table by `app.current_tenant_id`. The migrator sets `app.is_app_master_admin = 'true'` via `SET LOCAL` for the upgrade transaction — the same bypass `[AllowCrossTenant]` routes use. `SET LOCAL` is bounded by the transaction, so a crash mid-upgrade cannot leak the bypass back into the connection pool.

## License

MIT.
