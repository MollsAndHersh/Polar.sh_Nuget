namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Fine-grained permissions granted via role membership and gated by <c>[RequirePolarPermission]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Permissions split into two scopes:
/// </para>
/// <list type="bullet">
///   <item><description><strong>Tenant-scoped</strong> — granted by tenant-level role assignment via <c>PolarUserTenantMembership</c>; checked against the user's current tenant context.</description></item>
///   <item><description><strong>Site-level</strong> — granted ONLY by <see cref="PolarRoles.AppMasterAdmin"/>; checked against the user's <c>IsAppMasterAdmin</c> claim.</description></item>
/// </list>
/// <para>
/// The default role-to-permission mapping is seeded by <c>RoleSeeder</c> and lives in
/// <c>RolePermissionMap</c>. Hosts can extend the mapping for custom roles via the
/// <c>AddPolarIdentity(...)</c> options.
/// </para>
/// </remarks>
public enum PolarPermission
{
    // ── Catalog (tenant-scoped) ────────────────────────────────────────

    /// <summary>Edit local catalog entities (products, categories, tier groups, variants).</summary>
    EditCatalog,

    /// <summary>Push local catalog changes to Polar.sh.</summary>
    PublishCatalog,

    /// <summary>Archive (soft-delete) a Polar product.</summary>
    ArchiveProduct,

    // ── Operations (tenant-scoped) ────────────────────────────────────

    /// <summary>Issue full or partial refunds against Polar orders.</summary>
    IssueRefund,

    /// <summary>Cancel an active subscription on behalf of a customer.</summary>
    CancelSubscription,

    /// <summary>Adjust SKU inventory counts and stock-status overrides.</summary>
    ManageInventory,

    // ── Configuration (tenant-scoped) ─────────────────────────────────

    /// <summary>Initiate or update the tenant's Stripe Connect / payout setup.</summary>
    ConfigureBanking,

    /// <summary>Edit the tenant's business profile (legal name, KYC fields, address).</summary>
    EditBusinessProfile,

    /// <summary>Add or remove user memberships within the tenant.</summary>
    ManageMembers,

    /// <summary>Create custom roles or modify the role-permission mapping for the tenant.</summary>
    ManageRoles,

    // ── Reporting (tenant-scoped) ──────────────────────────────────────

    /// <summary>View pre-built reports for the tenant.</summary>
    ViewReports,

    /// <summary>Export reports as CSV or JSON.</summary>
    ExportReports,

    /// <summary>View the tenant's admin audit log.</summary>
    ViewAuditLog,

    // ── Onboarding (tenant-scoped UNLESS escalated to AppMasterAdmin) ──

    /// <summary>Provision a new tenant (via OAuth-link or programmatic onboarding).</summary>
    CreateTenant,

    /// <summary>Soft-delete or archive a tenant.</summary>
    DeleteTenant,

    // ── Data seeding (tenant-scoped) ───────────────────────────────────

    /// <summary>Generate fake catalog data for QA / sandbox use.</summary>
    SeedFakeData,

    /// <summary>Toggle the tenant-wide <c>AllowFakeData</c> flag.</summary>
    ToggleFakeData,

    // ── Benefits / discounts (tenant-scoped) ───────────────────────────

    /// <summary>Manage the tenant's Polar benefit definitions.</summary>
    ManageBenefits,

    /// <summary>Manage the tenant's discount and coupon-code definitions.</summary>
    ManageDiscounts,

    /// <summary>Manage the tenant's checkout-link configurations.</summary>
    ManageCheckoutLinks,

    // ── SITE-LEVEL (AppMasterAdmin only) ───────────────────────────────

    /// <summary>SITE-LEVEL — explicitly opt into a cross-tenant operation. Required co-attribute on routes that bypass tenant scope.</summary>
    CrossTenantAccess,

    /// <summary>SITE-LEVEL — grant or revoke <see cref="PolarRoles.AppMasterAdmin"/> from another user.</summary>
    ManageAppMasterAdmins,

    /// <summary>SITE-LEVEL — view the platform-wide audit log of cross-tenant operations.</summary>
    ViewPlatformAuditLog,
}
