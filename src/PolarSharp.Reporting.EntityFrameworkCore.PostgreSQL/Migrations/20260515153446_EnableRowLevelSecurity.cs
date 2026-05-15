using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL.Migrations
{
    /// <summary>
    /// V20-008 Layer 2: enables PostgreSQL row-level security on all 15 <c>ITenantOwned</c>
    /// reporting snapshot tables. Pairs with <c>PostgreSqlTenantSessionInterceptor</c>.
    /// </summary>
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE polar_report_events                ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_events                FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_orders                ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_orders                FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_order_line_items      ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_order_line_items      FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_order_refunds         ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_order_refunds         FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_subscriptions         ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_subscriptions         FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_customers             ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_customers             FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_benefit_grants        ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_benefit_grants        FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_benefits              ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_benefits              FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_discounts             ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_discounts             FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_checkout_links        ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_checkout_links        FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_products              ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_products              FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_license_keys          ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_license_keys          FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_meters                ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_meters                FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_customer_meters       ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_customer_meters       FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_snapshot_checkpoints  ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_report_snapshot_checkpoints  FORCE ROW LEVEL SECURITY;

                CREATE POLICY tenant_isolation ON polar_report_events                USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_orders                USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_order_line_items      USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_order_refunds         USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_subscriptions         USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_customers             USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_benefit_grants        USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_benefits              USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_discounts             USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_checkout_links        USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_products              USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_license_keys          USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_meters                USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_customer_meters       USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_report_snapshot_checkpoints  USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_events;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_orders;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_order_line_items;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_order_refunds;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_subscriptions;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_customers;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_benefit_grants;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_benefits;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_discounts;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_checkout_links;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_products;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_license_keys;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_meters;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_customer_meters;
                DROP POLICY IF EXISTS tenant_isolation ON polar_report_snapshot_checkpoints;

                ALTER TABLE polar_report_events                NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_events                DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_orders                NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_orders                DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_order_line_items      NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_order_line_items      DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_order_refunds         NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_order_refunds         DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_subscriptions         NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_subscriptions         DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_customers             NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_customers             DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_benefit_grants        NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_benefit_grants        DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_benefits              NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_benefits              DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_discounts             NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_discounts             DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_checkout_links        NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_checkout_links        DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_products              NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_products              DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_license_keys          NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_license_keys          DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_meters                NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_meters                DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_customer_meters       NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_customer_meters       DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_report_snapshot_checkpoints  NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_report_snapshot_checkpoints  DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
