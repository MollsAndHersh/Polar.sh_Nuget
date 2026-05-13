using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.MultiTenant.Identity.Tests;

/// <summary>
/// Verifies the default resolver returns the documented permission sets and that host-supplied
/// overrides take precedence without losing default mappings for unrelated roles.
/// </summary>
public sealed class RolePermissionResolverTests
{
    [Fact]
    public void Default_resolver_returns_TenantAdmin_permissions()
    {
        var resolver = new DefaultRolePermissionResolver();
        var perms = resolver.PermissionsForRole(PolarRoles.TenantAdmin);
        Assert.Contains(PolarPermission.EditCatalog, perms);
        Assert.Contains(PolarPermission.IssueRefund, perms);
        Assert.DoesNotContain(PolarPermission.CrossTenantAccess, perms);
    }

    [Fact]
    public void Unknown_role_returns_empty_permission_set()
    {
        var resolver = new DefaultRolePermissionResolver();
        var perms = resolver.PermissionsForRole("Custom.NotARealRole");
        Assert.Empty(perms);
    }

    [Fact]
    public void Host_override_replaces_default_for_specified_role_only()
    {
        IDictionary<string, IReadOnlySet<PolarPermission>> overrides = new Dictionary<string, IReadOnlySet<PolarPermission>>
        {
            [PolarRoles.ReadOnly] = new HashSet<PolarPermission>
            {
                PolarPermission.ViewReports,
                PolarPermission.ExportReports,   // expanded ReadOnly to also export
            },
        };

        var resolver = new DefaultRolePermissionResolver(overrides);

        var readOnlyPerms = resolver.PermissionsForRole(PolarRoles.ReadOnly);
        Assert.Contains(PolarPermission.ExportReports, readOnlyPerms);

        // Other roles still use defaults
        var auditorPerms = resolver.PermissionsForRole(PolarRoles.Auditor);
        Assert.Contains(PolarPermission.ViewAuditLog, auditorPerms);
    }

    [Fact]
    public void Override_can_introduce_a_brand_new_role()
    {
        IDictionary<string, IReadOnlySet<PolarPermission>> overrides = new Dictionary<string, IReadOnlySet<PolarPermission>>
        {
            ["MyApp.SupportAgent"] = new HashSet<PolarPermission>
            {
                PolarPermission.ViewReports,
                PolarPermission.IssueRefund,
            },
        };

        var resolver = new DefaultRolePermissionResolver(overrides);

        var supportPerms = resolver.PermissionsForRole("MyApp.SupportAgent");
        Assert.Equal(2, supportPerms.Count);
        Assert.Contains(PolarPermission.IssueRefund, supportPerms);
    }
}
