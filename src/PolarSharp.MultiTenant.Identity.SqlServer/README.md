# PolarSharp.MultiTenant.Identity.SqlServer

SQL Server provider for `PolarSharp.MultiTenant.Identity`. Adds Identity-specific EF Core migrations on top of the multi-tenant SQL Server tenant store.

## Install

```bash
dotnet add package PolarSharp.MultiTenant.Identity.SqlServer
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant().UseSqlServer(tenantConnStr)
    .AddPolarIdentity().UseSqlServer(identityConnStr);
```

Identity tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, `AspNetUserRoles`, plus PolarSharp's `PolarUserTenantMembership`) share the same Row-Level Security pattern as catalog tables. See `docs/articles/identity-and-authorization.md` for the cross-tenant safeguard architecture.

## License

MIT.
