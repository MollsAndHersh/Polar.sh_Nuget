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

## License

MIT.
