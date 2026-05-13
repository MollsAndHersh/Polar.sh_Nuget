# PolarSharp.MultiTenant.EntityFrameworkCore

EF Core-backed tenant store for [PolarSharp.MultiTenant](https://www.nuget.org/packages/PolarSharp.MultiTenant). Provider-agnostic base — install one of the companion packages to select a backing database:

- `PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer`
- `PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite`
- `PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL`

## Install

```bash
dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseSqlServer(connectionString);
```

The `appsettings.json:PolarSharp:MultiTenant:Tenants` array (from v1.1.0) continues to work — install this package only when you want SQL-backed tenant storage. See `docs/articles/sql-tenant-store.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/) for full setup, RLS migrations, distributed cache configuration, and migration from appsettings.

## What's in this base package

- `TenantAwareDbContextBase` — applies tenant + IsFakeData global query filters and SaveChanges stamping
- `ITenantOwned`, `IFakeDataAware` interface contracts
- `PolarTenantDbContext` — Identity-free tenant store DbContext
- `EfMultiTenantStore` — `IMultiTenantStore<PolarTenantInfo>` backed by EF Core
- `IPolarTenantCache` (Memory default; pluggable `IDistributedCache` wrapper)
- `IPolarTenantScopeInitializer` — shared scope hydration used by webhooks and reporting

## License

MIT — see [LICENSE](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/LICENSE).
