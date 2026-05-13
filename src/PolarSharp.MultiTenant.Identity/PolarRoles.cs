namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Built-in role names recognized by PolarSharp's authorization pipeline.
/// </summary>
/// <remarks>
/// <para>
/// One site-level role (<see cref="AppMasterAdmin"/>) and four tenant-level roles. Hosts can
/// register additional custom roles via the standard ASP.NET Core <see cref="Microsoft.AspNetCore.Identity.RoleManager{TRole}"/>
/// API; the built-in set covers the canonical SaaS RBAC matrix.
/// </para>
/// <para>
/// Built-in roles are seeded on first startup by <c>RoleSeeder</c>. The
/// <see cref="AppMasterAdmin"/> role is special — it is the only role marked
/// <c>IsSiteLevel = true</c> and the only role permitted to bypass tenant scope (and only
/// when the route opts in via the <c>[AllowCrossTenant]</c> attribute).
/// </para>
/// </remarks>
public static class PolarRoles
{
    /// <summary>SITE-LEVEL — SaaS provider's own staff. Bypasses tenant scope only on routes annotated with <c>[AllowCrossTenant]</c>.</summary>
    public const string AppMasterAdmin = "PolarSharp.AppMasterAdmin";

    /// <summary>TENANT-LEVEL — full administrative access within ONE tenant membership.</summary>
    public const string TenantAdmin = "PolarSharp.TenantAdmin";

    /// <summary>TENANT-LEVEL — day-to-day operational access within ONE tenant membership.</summary>
    public const string TenantUser = "PolarSharp.TenantUser";

    /// <summary>TENANT-LEVEL — read-only access within ONE tenant membership.</summary>
    public const string ReadOnly = "PolarSharp.ReadOnly";

    /// <summary>TENANT-LEVEL — read access plus audit log inspection within ONE tenant membership.</summary>
    public const string Auditor = "PolarSharp.Auditor";

    /// <summary>The full set of built-in role names — enumerated for seeding and validation.</summary>
    public static IReadOnlyList<string> All { get; } = [AppMasterAdmin, TenantAdmin, TenantUser, ReadOnly, Auditor];

    /// <summary>The subset of roles that operate within a tenant scope (excludes <see cref="AppMasterAdmin"/>).</summary>
    public static IReadOnlyList<string> TenantScoped { get; } = [TenantAdmin, TenantUser, ReadOnly, Auditor];
}
