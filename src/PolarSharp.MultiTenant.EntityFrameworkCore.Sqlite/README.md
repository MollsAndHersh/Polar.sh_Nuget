# PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite

SQLite provider for PolarSharp's EF Core-backed tenant store. Uses **physical file isolation** — each tenant's catalog/identity DB lives in its own `{tenantId}.db` file. Platform-level data (the tenant registry and the upgrade-history table) lives in a shared `master_SaaS.db`.

## Install

```bash
dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseSqlite("/var/lib/polarsharp/tenants/");
```

Directory contents after onboarding two tenants:

```
/var/lib/polarsharp/tenants/
├── master_SaaS.db                                         ← platform data (registry + upgrade history)
├── 3f7c2a4e-9d8b-4e15-a7c1-1b2c3d4e5f6a.db              ← Tenant A's catalog/identity
└── b81c5d2f-0e2a-4b6c-9d8e-5f6a7b8c9d0e.db              ← Tenant B's catalog/identity
```

The shared `master_SaaS.db` opens in WAL journal mode by default — required by Litestream replication (Stage C) and the right default for any non-trivial workload. Per-tenant DBs use the default journal mode.

## Platform data: `master_SaaS.db`

The master file holds everything that describes the deployment rather than any one tenant's data:

- The tenant registry (`polar_tenants`)
- The one-time upgrade-history table (`polar_upgrade_history`)
- Future cross-tenant platform tables as they ship

Nothing tenant-owned ever touches `master_SaaS.db`. The naming makes the platform / tenant boundary obvious to operators inspecting the filesystem.

### Migrating from `__tenants.db`

The pre-v1.2 SQLite provider used `__tenants.db` for the registry. When the provider starts up and finds a legacy `__tenants.db` (and no `master_SaaS.db`), it falls back to the legacy filename for the current run and logs a warning. Rename the file during a maintenance window — either manually, or by running the single-tenant upgrade migrator (see below), which renames as part of its work.

## Single-tenant -> multi-tenant upgrade

Hosts that started life as single-tenant deployments can opt into the automated upgrade migrator:

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseSqlite("/var/lib/polarsharp/tenants/");

builder.Services.AddPolarSingleTenantUpgrade(builder.Configuration);
```

On the next boot, the migrator:

1. Confirms `master_SaaS.db` is in place (using the legacy fallback when needed).
2. Inserts the default tenant into the registry — or recognises an existing single-tenant entry as the default.
3. Renames any legacy `data.db` or `app.db` in the database directory to `{defaultTenantId}.db`.
4. Records a row in `polar_upgrade_history` so subsequent boots short-circuit.

The migrator never deletes anything. A surviving `__tenants.db` alongside `master_SaaS.db` triggers a warning recommending manual review.

See `docs/articles/tenant-isolation.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/) for the full file-isolation rationale and SQLite-specific tuning.

## License

MIT.
