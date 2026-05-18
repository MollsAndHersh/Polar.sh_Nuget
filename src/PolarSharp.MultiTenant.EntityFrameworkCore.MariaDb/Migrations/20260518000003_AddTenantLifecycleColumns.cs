using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb.Migrations
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
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SiteManagerEmail",
                table: "polar_tenants",
                type: "varchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SiteManagerEmailVerified",
                table: "polar_tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SiteManagerPhone",
                table: "polar_tenants",
                type: "varchar(32)",
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
