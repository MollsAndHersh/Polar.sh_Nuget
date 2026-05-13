using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.MultiTenant.Identity.Tests;

/// <summary>
/// Verifies the default role-to-permission mapping is consistent with the documented
/// matrix and that the site-level vs tenant-level distinction is clean.
/// </summary>
public sealed class RolePermissionMapTests
{
    [Fact]
    public void AppMasterAdmin_grants_every_permission()
    {
        var perms = RolePermissionMap.Defaults[PolarRoles.AppMasterAdmin];
        foreach (var p in Enum.GetValues<PolarPermission>())
        {
            Assert.Contains(p, perms);
        }
    }

    [Fact]
    public void TenantAdmin_grants_no_site_level_permissions()
    {
        var perms = RolePermissionMap.Defaults[PolarRoles.TenantAdmin];
        Assert.DoesNotContain(PolarPermission.CrossTenantAccess, perms);
        Assert.DoesNotContain(PolarPermission.ManageAppMasterAdmins, perms);
        Assert.DoesNotContain(PolarPermission.ViewPlatformAuditLog, perms);
    }

    [Fact]
    public void TenantUser_does_not_have_member_management()
    {
        var perms = RolePermissionMap.Defaults[PolarRoles.TenantUser];
        Assert.DoesNotContain(PolarPermission.ManageMembers, perms);
        Assert.DoesNotContain(PolarPermission.ManageRoles, perms);
        Assert.DoesNotContain(PolarPermission.ConfigureBanking, perms);
    }

    [Fact]
    public void ReadOnly_only_views_reports()
    {
        var perms = RolePermissionMap.Defaults[PolarRoles.ReadOnly];
        Assert.Single(perms);
        Assert.Contains(PolarPermission.ViewReports, perms);
    }

    [Fact]
    public void Auditor_can_view_audit_log_but_cannot_edit_catalog()
    {
        var perms = RolePermissionMap.Defaults[PolarRoles.Auditor];
        Assert.Contains(PolarPermission.ViewAuditLog, perms);
        Assert.DoesNotContain(PolarPermission.EditCatalog, perms);
    }

    [Theory]
    [InlineData(PolarPermission.CrossTenantAccess, true)]
    [InlineData(PolarPermission.ManageAppMasterAdmins, true)]
    [InlineData(PolarPermission.ViewPlatformAuditLog, true)]
    [InlineData(PolarPermission.EditCatalog, false)]
    [InlineData(PolarPermission.IssueRefund, false)]
    [InlineData(PolarPermission.ViewReports, false)]
    public void IsSiteLevel_matches_documented_categorization(PolarPermission permission, bool expected)
    {
        Assert.Equal(expected, RolePermissionMap.IsSiteLevel(permission));
    }

    [Fact]
    public void All_built_in_role_names_are_in_the_All_collection()
    {
        Assert.Contains(PolarRoles.AppMasterAdmin, PolarRoles.All);
        Assert.Contains(PolarRoles.TenantAdmin, PolarRoles.All);
        Assert.Contains(PolarRoles.TenantUser, PolarRoles.All);
        Assert.Contains(PolarRoles.ReadOnly, PolarRoles.All);
        Assert.Contains(PolarRoles.Auditor, PolarRoles.All);
        Assert.Equal(5, PolarRoles.All.Count);
    }

    [Fact]
    public void TenantScoped_excludes_AppMasterAdmin()
    {
        Assert.DoesNotContain(PolarRoles.AppMasterAdmin, PolarRoles.TenantScoped);
        Assert.Equal(4, PolarRoles.TenantScoped.Count);
    }
}
