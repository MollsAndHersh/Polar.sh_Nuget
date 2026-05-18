using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb.Migrations
{
    /// <inheritdoc />
    public partial class AddUpgradeHistoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "polar_upgrade_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UpgradeKind = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    ActorUserId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    Message = table.Column<string>(type: "longtext", nullable: true),
                    ResultSummaryJson = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_upgrade_history", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_polar_upgrade_history_UpgradeKind",
                table: "polar_upgrade_history",
                column: "UpgradeKind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "polar_upgrade_history");
        }
    }
}
