using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLifecycleColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LifecycleStatus",
                table: "polar_tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SiteManagerEmail",
                table: "polar_tenants",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SiteManagerEmailVerified",
                table: "polar_tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SiteManagerPhone",
                table: "polar_tenants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LifecycleStatus", table: "polar_tenants");
            migrationBuilder.DropColumn(name: "SiteManagerEmail", table: "polar_tenants");
            migrationBuilder.DropColumn(name: "SiteManagerEmailVerified", table: "polar_tenants");
            migrationBuilder.DropColumn(name: "SiteManagerPhone", table: "polar_tenants");
        }
    }
}
