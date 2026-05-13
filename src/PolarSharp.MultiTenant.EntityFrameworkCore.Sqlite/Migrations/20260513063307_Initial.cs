using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "polar_tenants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PolarAccessToken = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Server = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    WebhookEndpointId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    WebhookSecret = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    Website = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    AvatarUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    DefaultPresentmentCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PayoutAccountId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_polar_tenants_Identifier",
                table: "polar_tenants",
                column: "Identifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "polar_tenants");
        }
    }
}
