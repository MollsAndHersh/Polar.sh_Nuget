using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.Reporting.EntityFrameworkCore.SqlServer.Migrations
{
    /// <summary>
    /// V20-008 Layer 2: enables SQL Server row-level security on all 15 <c>ITenantOwned</c>
    /// reporting snapshot tables. Pairs with <c>SqlServerTenantSessionInterceptor</c>.
    /// </summary>
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE FUNCTION [dbo].[fn_polar_reporting_tenant_filter](@TenantId NVARCHAR(450))
                    RETURNS TABLE WITH SCHEMABINDING
                    AS RETURN SELECT 1 AS result
                    WHERE @TenantId = CAST(SESSION_CONTEXT(N'tenant_id') AS NVARCHAR(450))
                       OR CAST(SESSION_CONTEXT(N'is_app_master_admin') AS BIT) = 1;
            ");

            migrationBuilder.Sql(@"
                CREATE SECURITY POLICY [dbo].[polar_reporting_tenant_security_policy]
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_events],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_events] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_events] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_orders],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_orders] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_orders] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_order_line_items],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_order_line_items] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_order_line_items] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_order_refunds],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_order_refunds] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_order_refunds] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_subscriptions],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_subscriptions] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_subscriptions] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_customers],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_customers] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_customers] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_benefit_grants],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_benefit_grants] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_benefit_grants] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_benefits],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_benefits] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_benefits] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_discounts],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_discounts] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_discounts] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_checkout_links],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_checkout_links] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_checkout_links] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_products],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_products] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_products] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_license_keys],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_license_keys] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_license_keys] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_meters],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_meters] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_meters] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_customer_meters],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_customer_meters] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_customer_meters] AFTER UPDATE,
                    ADD FILTER PREDICATE [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_snapshot_checkpoints],
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_snapshot_checkpoints] AFTER INSERT,
                    ADD BLOCK PREDICATE  [dbo].[fn_polar_reporting_tenant_filter]([TenantId]) ON [dbo].[polar_report_snapshot_checkpoints] AFTER UPDATE
                WITH (STATE = ON);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SECURITY POLICY [dbo].[polar_reporting_tenant_security_policy];");
            migrationBuilder.Sql("DROP FUNCTION [dbo].[fn_polar_reporting_tenant_filter];");
        }
    }
}
