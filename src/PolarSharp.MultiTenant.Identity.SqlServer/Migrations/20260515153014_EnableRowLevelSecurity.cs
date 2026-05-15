using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.MultiTenant.Identity.SqlServer.Migrations
{
    /// <summary>
    /// V20-008 Layer 2: enables SQL Server row-level security on every <c>ITenantOwned</c>
    /// table in the Identity DbContext. Pairs with <c>SqlServerTenantSessionInterceptor</c>
    /// which sets <c>SESSION_CONTEXT(N'tenant_id')</c> + <c>SESSION_CONTEXT(N'is_app_master_admin')</c>
    /// per connection. Failing the predicate returns zero rows; AppMasterAdmin opt-in
    /// (via <c>[AllowCrossTenant]</c>) bypasses the filter.
    /// </summary>
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE FUNCTION [dbo].[fn_polar_identity_tenant_filter](@TenantId UNIQUEIDENTIFIER)
                    RETURNS TABLE WITH SCHEMABINDING
                    AS RETURN SELECT 1 AS result
                    WHERE CAST(@TenantId AS NVARCHAR(36)) = CAST(SESSION_CONTEXT(N'tenant_id') AS NVARCHAR(36))
                       OR CAST(SESSION_CONTEXT(N'is_app_master_admin') AS BIT) = 1;
            ");

            migrationBuilder.Sql(@"
                CREATE SECURITY POLICY [dbo].[polar_identity_tenant_security_policy]
                    ADD FILTER PREDICATE [dbo].[fn_polar_identity_tenant_filter]([TenantId]) ON [dbo].[polar_user_tenant_memberships],
                    ADD BLOCK PREDICATE [dbo].[fn_polar_identity_tenant_filter]([TenantId]) ON [dbo].[polar_user_tenant_memberships] AFTER INSERT,
                    ADD BLOCK PREDICATE [dbo].[fn_polar_identity_tenant_filter]([TenantId]) ON [dbo].[polar_user_tenant_memberships] AFTER UPDATE
                WITH (STATE = ON);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SECURITY POLICY [dbo].[polar_identity_tenant_security_policy];");
            migrationBuilder.Sql("DROP FUNCTION [dbo].[fn_polar_identity_tenant_filter];");
        }
    }
}
