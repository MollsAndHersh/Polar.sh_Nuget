# PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite

SQLite provider for PolarSharp's EF Core-backed tenant store. Uses **physical file isolation** — each tenant's catalog/identity DB lives in its own `{tenantId}.db` file. The bootstrap tenant store lives in a shared `__tenants.db`.

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
├── __tenants.db                                          ← tenant registry (shared)
├── 3f7c2a4e-9d8b-4e15-a7c1-1b2c3d4e5f6a.db              ← Tenant A's catalog/identity
└── b81c5d2f-0e2a-4b6c-9d8e-5f6a7b8c9d0e.db              ← Tenant B's catalog/identity
```

The shared `__tenants.db` uses WAL mode for safe multi-process access; per-tenant DBs use the default journal mode.

See `docs/articles/tenant-isolation.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/) for the full file-isolation rationale and SQLite-specific tuning.

## License

MIT.
