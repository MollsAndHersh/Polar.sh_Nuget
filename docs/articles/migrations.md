# EF Core Migrations

Every SQL-backed PolarSharp package ships **real EF Core migration files** in each provider package. Hosts get a deterministic, idempotent, version-aware path to bring up the schema — no `EnsureCreatedAsync` in Production.

## Coverage

12 migration sets, one per (DbContext × provider) combination:

| DbContext | SqlServer | Sqlite | PostgreSQL |
|---|---|---|---|
| `PolarTenantDbContext` | ✅ | ✅ | ✅ |
| `PolarUserDbContext` | ✅ | ✅ | ✅ |
| `PolarCatalogDbContext` | ✅ | ✅ | ✅ |
| `PolarReportingDbContext` | ✅ | ✅ | ✅ |

## How migrations apply at runtime

Register `PolarMigrationRunner<TContext>` for each DbContext in your host:

```csharp
builder.Services.RunPolarMigrationsAtStartup<PolarTenantDbContext>();
builder.Services.RunPolarMigrationsAtStartup<PolarUserDbContext>();
builder.Services.RunPolarMigrationsAtStartup<PolarCatalogDbContext>();
builder.Services.RunPolarMigrationsAtStartup<PolarReportingDbContext>();
```

On host startup, the runner:

1. Checks the DbContext for registered migrations
2. **In Production**, throws if none are registered (refuses to silently `EnsureCreated` an unversioned schema)
3. **In Development**, falls back to `EnsureCreatedAsync` when no migrations exist AND `PolarMigrationOptions.UseEnsureCreatedInDevelopment = true` (default)
4. Otherwise calls `Database.MigrateAsync()` — idempotent; re-runs are no-ops when the schema is up to date

## Adding a new migration to your own host

If you've extended a PolarSharp entity (e.g., added columns to `PolarApplicationUser` via inheritance), generate a host-side migration:

```sh
dotnet ef migrations add MyHostAddition \
  --context PolarUserDbContext \
  --project src/MyHost \
  --output-dir Migrations
```

The migration assembly is set per provider via `MigrationsAssembly(...)` in each `UseXxx()` extension — your host's migration project must opt-in via the same mechanism if you want host-side migrations to coexist.

## CI verification gate

Every release candidate must pass:

```sh
dotnet ef migrations script --idempotent --project src/PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer    --context PolarTenantDbContext
dotnet ef migrations script --idempotent --project src/PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite       --context PolarTenantDbContext
dotnet ef migrations script --idempotent --project src/PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL   --context PolarTenantDbContext
# ... and the same for PolarUserDbContext, PolarCatalogDbContext, PolarReportingDbContext
```

A missing or invalid migration fails CI before the package is allowed to publish.

## Production checklist

- [ ] `PolarMigrationRunner<TContext>` registered for every DbContext the host uses
- [ ] `PolarMigrationOptions.UseEnsureCreatedInDevelopment = false` in `appsettings.Production.json`
- [ ] Migration scripts inspected (`dotnet ef migrations script` → review) before applying to production DBs
- [ ] Host has rollback procedure for failed `Database.MigrateAsync()` (typically: re-deploy the previous build that's compatible with the current schema)
