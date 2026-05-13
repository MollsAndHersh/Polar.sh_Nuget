# PolarSharp.MultiTenant.Identity

ASP.NET Core Identity integration for PolarSharp multi-tenant SaaS deployments. Provider-agnostic base — install one of:

- `PolarSharp.MultiTenant.Identity.SqlServer`
- `PolarSharp.MultiTenant.Identity.Sqlite`
- `PolarSharp.MultiTenant.Identity.PostgreSQL`

Optional SSO add-on: `PolarSharp.MultiTenant.Identity.KeyCloak`.

## Install

```bash
dotnet add package PolarSharp.MultiTenant.Identity.SqlServer
```

## What it provides

- **`PolarApplicationUser : IdentityUser<Guid>`** + **`PolarApplicationRole : IdentityRole<Guid>`** — all IDs are GUIDs
- **`PolarUserTenantMembership`** — M:N join with per-tenant role assignment (a user can be `TenantAdmin` in Tenant A and `ReadOnly` in Tenant B)
- **Two role tiers**: site-level `AppMasterAdmin` (SaaS staff with cross-tenant access) and tenant-level `TenantAdmin` / `TenantUser` / `ReadOnly` / `Auditor`
- **`PolarPermission` enum** + **`[RequirePolarPermission(...)]`** attribute (tenant-scoped) + **`[RequireAppMasterAdmin]`** + **`[AllowCrossTenant]`** opt-in for cross-tenant ops
- **Five-layer safeguards**: EF query filter, database RLS, attribute composition, self-elevation prevention via `IAppMasterAdminProvisioning`, dual audit log (tenant + platform)
- **`AppMasterAdminBootstrapper`** seeds the first platform admin from `appsettings.json` on first startup; Production blocks until reset is completed
- **`TenantAdminInvariantValidator`** ensures every tenant has at least one active `TenantAdmin` membership

See `docs/articles/identity-and-authorization.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/) for the complete authorization architecture.

## License

MIT.
