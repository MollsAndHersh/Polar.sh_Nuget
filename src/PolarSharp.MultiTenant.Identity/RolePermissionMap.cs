namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Default role-to-permission mapping for the built-in PolarSharp roles.
/// </summary>
/// <remarks>
/// <para>
/// Hosts can override per-role permission sets in <c>AddPolarIdentity(opts =&gt; ...)</c>; the
/// values here are the safe defaults that ship with the package.
/// </para>
/// </remarks>
public static class RolePermissionMap
{
    /// <summary>The complete default mapping. Key = role name; value = permission set granted by that role.</summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<PolarPermission>> Defaults { get; } =
        new Dictionary<string, IReadOnlySet<PolarPermission>>(StringComparer.Ordinal)
        {
            // SITE-LEVEL — every permission, plus the three site-only gates.
            [PolarRoles.AppMasterAdmin] = new HashSet<PolarPermission>(Enum.GetValues<PolarPermission>()),

            [PolarRoles.TenantAdmin] = new HashSet<PolarPermission>
            {
                PolarPermission.EditCatalog,
                PolarPermission.PublishCatalog,
                PolarPermission.ArchiveProduct,
                PolarPermission.IssueRefund,
                PolarPermission.CancelSubscription,
                PolarPermission.ManageInventory,
                PolarPermission.ConfigureBanking,
                PolarPermission.EditBusinessProfile,
                PolarPermission.ManageMembers,
                PolarPermission.ManageRoles,
                PolarPermission.ViewReports,
                PolarPermission.ExportReports,
                PolarPermission.ViewAuditLog,
                PolarPermission.SeedFakeData,
                PolarPermission.ToggleFakeData,
                PolarPermission.ManageBenefits,
                PolarPermission.ManageDiscounts,
                PolarPermission.ManageCheckoutLinks,
            },

            [PolarRoles.TenantUser] = new HashSet<PolarPermission>
            {
                PolarPermission.EditCatalog,
                PolarPermission.PublishCatalog,
                PolarPermission.IssueRefund,
                PolarPermission.ManageInventory,
                PolarPermission.ViewReports,
                PolarPermission.SeedFakeData,
                PolarPermission.ManageBenefits,
                PolarPermission.ManageDiscounts,
            },

            [PolarRoles.ReadOnly] = new HashSet<PolarPermission>
            {
                PolarPermission.ViewReports,
            },

            [PolarRoles.Auditor] = new HashSet<PolarPermission>
            {
                PolarPermission.ViewReports,
                PolarPermission.ViewAuditLog,
                PolarPermission.ExportReports,
            },
        };

    /// <summary>Returns true if the supplied permission is in the SITE-LEVEL category — gateable only by <see cref="PolarRoles.AppMasterAdmin"/>.</summary>
    public static bool IsSiteLevel(PolarPermission permission) => permission switch
    {
        PolarPermission.CrossTenantAccess => true,
        PolarPermission.ManageAppMasterAdmins => true,
        PolarPermission.ViewPlatformAuditLog => true,
        _ => false,
    };
}
