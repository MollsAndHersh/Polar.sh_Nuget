# PolarSharp.MultiTenant.Identity.PostgreSQL

PostgreSQL provider for `PolarSharp.MultiTenant.Identity`. Adds Identity-specific EF Core migrations on top of the multi-tenant PostgreSQL tenant store with RLS.

## Install

```bash
dotnet add package PolarSharp.MultiTenant.Identity.PostgreSQL
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant().UsePostgreSql(tenantConnStr)
    .AddPolarIdentity().UsePostgreSql(identityConnStr);
```

Identity tables share the same `FORCE ROW LEVEL SECURITY` and `CREATE POLICY tenant_isolation` pattern as catalog tables. See `docs/articles/identity-and-authorization.md`.

## License

MIT.
