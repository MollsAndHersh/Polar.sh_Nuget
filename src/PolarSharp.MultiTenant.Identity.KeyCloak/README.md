# PolarSharp.MultiTenant.Identity.KeyCloak

Optional SSO add-on for `PolarSharp.MultiTenant.Identity`. Wraps `Microsoft.AspNetCore.Authentication.OpenIdConnect` for KeyCloak realm authentication. Maps KeyCloak realm roles to PolarSharp roles; emits the `is_app_master_admin` claim for SaaS platform staff.

## Install

```bash
dotnet add package PolarSharp.MultiTenant.Identity.KeyCloak
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant().UseSqlServer(connStr)
    .AddPolarIdentity().UseSqlServer(connStr)
    .AddPolarKeyCloak(opts =>
    {
        opts.Authority = "https://keycloak.example.com/realms/myrealm";
        opts.ClientId = "polarsharp-client";
        opts.ClientSecretEnvVar = "KEYCLOAK_CLIENT_SECRET";
        opts.RoleMapping["realm-admin"] = PolarRoles.AppMasterAdmin;
        opts.RoleMapping["realm-merchant"] = PolarRoles.TenantAdmin;
    });
```

When KeyCloak is the source of truth for credentials + role membership, PolarSharp continues to track per-user application state (last login, audit trail, M:N memberships) in its own SQL store.

See `docs/articles/keycloak-sso.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/) for realm import, attribute mapper configuration, and troubleshooting.

## License

MIT.
