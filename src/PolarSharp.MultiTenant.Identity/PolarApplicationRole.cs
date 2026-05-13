using Microsoft.AspNetCore.Identity;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// PolarSharp's <see cref="IdentityRole{Guid}"/> extension — represents a named role grant.
/// </summary>
/// <remarks>
/// <para>
/// Roles fall into two tiers:
/// </para>
/// <list type="bullet">
///   <item><description><strong>Site-level</strong> (<see cref="IsSiteLevel"/> = <see langword="true"/>): only <see cref="PolarRoles.AppMasterAdmin"/>. Membership is direct on <see cref="PolarApplicationUser"/> via the standard ASP.NET Core <see cref="UserManager{TUser}.AddToRoleAsync(TUser, string)"/> API and gates site-level permissions like cross-tenant access and platform-audit-log viewing.</description></item>
///   <item><description><strong>Tenant-level</strong> (<see cref="IsSiteLevel"/> = <see langword="false"/>): <see cref="PolarRoles.TenantAdmin"/>, <see cref="PolarRoles.TenantUser"/>, <see cref="PolarRoles.ReadOnly"/>, <see cref="PolarRoles.Auditor"/>. These are NOT assigned via ASP.NET Core's standard role table — they are referenced by FK from <see cref="PolarUserTenantMembership"/>, which scopes the assignment to a single tenant.</description></item>
/// </list>
/// <para>
/// Built-in roles are seeded on first startup with <see cref="IsBuiltIn"/> = <see langword="true"/> and cannot be deleted via the standard <c>RoleManager</c> APIs (deletion is blocked by <c>BuiltInRoleProtector</c>). Hosts can add custom roles by calling <c>RoleManager.CreateAsync(new PolarApplicationRole { Name = "...", IsBuiltIn = false })</c> directly.
/// </para>
/// </remarks>
public class PolarApplicationRole : IdentityRole<Guid>
{
    /// <summary>Default constructor — required by ASP.NET Core Identity.</summary>
    public PolarApplicationRole() { }

    /// <summary>Creates a role with the given name. Mirrors the standard <see cref="IdentityRole{TKey}"/> constructor.</summary>
    /// <param name="roleName">The unique role name (case-sensitive after normalization).</param>
    public PolarApplicationRole(string roleName) : base(roleName) { }

    /// <summary>Optional human-readable description of the role's purpose.</summary>
    public string? Description { get; set; }

    /// <summary>True for the built-in PolarSharp roles seeded at startup; protected from deletion.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>True ONLY for <see cref="PolarRoles.AppMasterAdmin"/> — distinguishes site-level from tenant-level role grants.</summary>
    public bool IsSiteLevel { get; set; }
}
