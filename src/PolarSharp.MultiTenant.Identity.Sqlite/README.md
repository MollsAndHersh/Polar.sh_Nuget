# PolarSharp.MultiTenant.Identity.Sqlite

SQLite provider for `PolarSharp.MultiTenant.Identity`. Identity tables live in each tenant's `{tenantId}.db` file alongside catalog data.

## Install

```bash
dotnet add package PolarSharp.MultiTenant.Identity.Sqlite
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant().UseSqlite("/var/lib/polarsharp/tenants/")
    .AddPolarIdentity().UseSqlite();
```

Per-tenant SQLite gives true filesystem-level isolation for user data — Tenant A's users physically cannot exist in Tenant B's `.db` file. The shared `__tenants.db` retains the global user-email uniqueness index for M:N support.

## License

MIT.
