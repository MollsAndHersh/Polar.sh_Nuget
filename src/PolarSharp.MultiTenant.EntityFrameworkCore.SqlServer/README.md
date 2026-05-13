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

## Migration from `appsettings.json`

```csharp
.UseSqlServer(connStr, seedFromAppSettings: true);
```

On first startup, copies all entries from `PolarSharp:MultiTenant:Tenants` into the database. Logs the seeded count. After seed completes, remove the `Tenants` array from `appsettings.json`.

## License

MIT.
