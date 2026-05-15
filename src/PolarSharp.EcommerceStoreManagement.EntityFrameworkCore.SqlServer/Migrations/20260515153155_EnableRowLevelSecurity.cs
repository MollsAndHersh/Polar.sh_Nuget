using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer.Migrations
{
    /// <summary>
    /// V20-008 Layer 2: enables SQL Server row-level security on all 12 <c>ITenantOwned</c>
    /// catalog tables. Pairs with <c>SqlServerTenantSessionInterceptor</c>.
    /// </summary>
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE FUNCTION [dbo].[fn_polar_catalog_tenant_filter](@TenantId NVARCHAR(450))
                    RETURNS TABLE WITH SCHEMABINDING
                    AS RETURN SELECT 1 AS result
                    WHERE @TenantId = CAST(SESSION_CONTEXT(N'tenant_id') AS NVARCHAR(450))
                       OR CAST(SESSION_CONTEXT(N'is_app_master_admin') AS BIT) = 1;
            ");

            migrationBuilder.Sql(@"
                CREATE SECURITY POLICY [dbo].[polar_catalog_tenant_security_policy]
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_business_profiles],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_business_profiles] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_business_profiles] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_products],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_products] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_products] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_product_categories],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_product_categories] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_product_categories] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_variants],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_variants] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_variants] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_categories],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_categories] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_categories] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_departments],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_departments] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_departments] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_tier_groups],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_tier_groups] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_tier_groups] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_benefits],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_benefits] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_benefits] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_discounts],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_discounts] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_discounts] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_checkout_links],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_checkout_links] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_local_checkout_links] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_admin_audit_log],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_admin_audit_log] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[polar_admin_audit_log] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[catalog_translations],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[catalog_translations] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_catalog_tenant_filter]([TenantId]) ON [dbo].[catalog_translations] AFTER UPDATE
                WITH (STATE = ON);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SECURITY POLICY [dbo].[polar_catalog_tenant_security_policy];");
            migrationBuilder.Sql("DROP FUNCTION [dbo].[fn_polar_catalog_tenant_filter];");
        }
    }
}
