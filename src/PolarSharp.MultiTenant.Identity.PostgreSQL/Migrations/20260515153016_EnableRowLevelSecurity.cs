using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.MultiTenant.Identity.PostgreSQL.Migrations
{
    /// <summary>
    /// V20-008 Layer 2: enables PostgreSQL row-level security on every <c>ITenantOwned</c>
    /// table in the Identity DbContext. Pairs with <c>PostgreSqlTenantSessionInterceptor</c>
    /// which sets <c>app.current_tenant_id</c> + <c>app.is_app_master_admin</c> per
    /// connection. Failing the policy filters all rows; AppMasterAdmin opt-in
    /// (via <c>[AllowCrossTenant]</c>) bypasses via the second clause.
    /// </summary>
    /// <remarks>
    /// <c>FORCE ROW LEVEL SECURITY</c> applies the policy even to the table OWNER (the
    /// connection user the migration runs under), preventing accidental bypass via
    /// privileged-role queries. Without FORCE, owners bypass RLS by default.
    /// </remarks>
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE polar_user_tenant_memberships ENABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_user_tenant_memberships FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON polar_user_tenant_memberships
                    USING (
                        ""TenantId""::text = current_setting('app.current_tenant_id', true)
                        OR current_setting('app.is_app_master_admin', true) = 'true'
                    );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation ON polar_user_tenant_memberships;
                ALTER TABLE polar_user_tenant_memberships NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_user_tenant_memberships DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
