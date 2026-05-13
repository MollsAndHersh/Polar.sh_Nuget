# Identity, Roles, and Cross-Tenant Safeguards

`PolarSharp.MultiTenant.Identity` integrates ASP.NET Core IdentityFramework into PolarSharp's multi-tenant model. Users are global identities (one email = one user); each user can hold a **different role in each tenant** via a many-to-many `PolarUserTenantMembership` join. The package ships built-in roles, fine-grained permissions, and **five layers of cross-tenant safeguards**.

## Roles

| Role | Tier | Purpose |
|---|---|---|
| `AppMasterAdmin` | **SITE-LEVEL** | SaaS-provider staff. Bypasses tenant scope only on routes annotated `[AllowCrossTenant]`. |
| `TenantAdmin` | TENANT | Full administrative access within ONE tenant membership. |
| `TenantUser` | TENANT | Day-to-day operational access within ONE tenant. |
| `ReadOnly` | TENANT | Read-only access within ONE tenant. |
| `Auditor` | TENANT | Read access plus audit-log inspection. |

## Permissions

22 fine-grained `PolarPermission` values cover catalog editing, refund issuance, member management, banking config, reporting, data seeding, benefit/discount management, plus 3 site-level permissions (`CrossTenantAccess`, `ManageAppMasterAdmins`, `ViewPlatformAuditLog`).

Gate routes with the typed attribute:

```csharp
[RequirePolarPermission(PolarPermission.IssueRefund)]
public async Task<IResult> IssueRefundAsync(...) { ... }
```

## Three deployment shapes

`PolarSharp.MultiTenant.Identity` adapts to whatever DB topology the host wants:

| Mode | When to use | How |
|---|---|---|
| **Dedicated DB, dedicated `PolarUserDbContext`** | Identity isolated from your app data | `.UseSqlServer("Server=...;Database=PolarIdentity;...")` |
| **Shared DB, dedicated `PolarUserDbContext`** | Single SQL Server with Identity + app tables coexisting | `.UseSqlServer(builder.Configuration)` reading `ConnectionStringName = "DefaultConnection"` |
| **Shared DB, your own `DbContext`** | One `DbContext` for the entire app | `.UseHostDbContext<ApplicationDbContext>()` + the host's context calls `ModelBuilder.AddPolarIdentitySchema()` in `OnModelCreating` |

## Five-layer cross-tenant safeguards

1. **EF Core query filter** on every `ITenantOwned` entity — automatically scopes reads to the current tenant; AppMasterAdmin bypass requires `[AllowCrossTenant]` opt-in
2. **SQL RLS policies** on SqlServer / PostgreSQL — defense at the database layer; catches raw-SQL bypass attempts
3. **Authorization attributes** (`[RequirePolarPermission]` / `[RequireAppMasterAdmin]` / `[AllowCrossTenant]`) — route-level gates
4. **Self-elevation prevention** — `IsAppMasterAdmin` flag NOT settable via any tenant-scoped API; only `IAppMasterAdminProvisioning` (which itself requires existing AppMasterAdmin); bootstrap path runs once at startup with a logged single-use reset token
5. **Dual audit log** — every cross-tenant operation writes to BOTH the target tenant's `AdminAuditLogEntry` AND the site-level `PlatformAuditLogEntry`

## `TenantAdmin` invariant

`TenantAdminInvariantValidator` runs on every startup and verifies every tenant has ≥1 active `TenantAdmin` membership. AppMasterAdmins do NOT count — each tenant must have its own dedicated administrator. Configurable to fail-fast or warn.

## AppMasterAdmin bootstrap

On first startup with no AppMasterAdmin in the DB, `AppMasterAdminBootstrapper` creates the seed user from `PolarSharp:Identity:Bootstrap:AppMasterAdminEmail` and logs a single-use password reset token at `LogLevel.Critical`. In Production, startup is **blocked** until the reset completes — preventing an unmanaged Production deployment.
