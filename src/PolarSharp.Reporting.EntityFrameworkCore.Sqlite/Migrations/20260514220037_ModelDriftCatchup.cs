using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.Reporting.EntityFrameworkCore.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ModelDriftCatchup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "polar_report_benefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    PolarBenefitId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_benefits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_checkout_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    PolarCheckoutLinkId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ProductIdsCsv = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    SuccessUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    AllowDiscountCodes = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_checkout_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_customer_meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    PolarCustomerMeterId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MeterId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConsumedUnits = table.Column<decimal>(type: "TEXT", precision: 20, scale: 4, nullable: false),
                    CreditedUnits = table.Column<decimal>(type: "TEXT", precision: 20, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_customer_meters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_discounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    PolarDiscountId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AmountOff = table.Column<long>(type: "INTEGER", nullable: true),
                    PercentOff = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    RedemptionsSoFar = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "INTEGER", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_discounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_license_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    PolarLicenseKeyId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BenefitId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DisplayKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LimitActivations = table.Column<int>(type: "INTEGER", nullable: true),
                    ActivationsUsed = table.Column<int>(type: "INTEGER", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_license_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    PolarMeterId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AggregationKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_meters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    PolarProductId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    IsRecurring = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecurringInterval = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_products", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_benefits_TenantId_Kind_IsActive",
                table: "polar_report_benefits",
                columns: new[] { "TenantId", "Kind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_benefits_TenantId_PolarBenefitId",
                table: "polar_report_benefits",
                columns: new[] { "TenantId", "PolarBenefitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_checkout_links_TenantId_CreatedAt",
                table: "polar_report_checkout_links",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_checkout_links_TenantId_PolarCheckoutLinkId",
                table: "polar_report_checkout_links",
                columns: new[] { "TenantId", "PolarCheckoutLinkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_customer_meters_TenantId_CustomerId_MeterId",
                table: "polar_report_customer_meters",
                columns: new[] { "TenantId", "CustomerId", "MeterId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_customer_meters_TenantId_PolarCustomerMeterId",
                table: "polar_report_customer_meters",
                columns: new[] { "TenantId", "PolarCustomerMeterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_discounts_TenantId_Code",
                table: "polar_report_discounts",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_discounts_TenantId_EndsAt",
                table: "polar_report_discounts",
                columns: new[] { "TenantId", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_discounts_TenantId_PolarDiscountId",
                table: "polar_report_discounts",
                columns: new[] { "TenantId", "PolarDiscountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_license_keys_TenantId_CustomerId",
                table: "polar_report_license_keys",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_license_keys_TenantId_PolarLicenseKeyId",
                table: "polar_report_license_keys",
                columns: new[] { "TenantId", "PolarLicenseKeyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_license_keys_TenantId_Status_ExpiresAt",
                table: "polar_report_license_keys",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_meters_TenantId_PolarMeterId",
                table: "polar_report_meters",
                columns: new[] { "TenantId", "PolarMeterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_products_TenantId_IsArchived",
                table: "polar_report_products",
                columns: new[] { "TenantId", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_products_TenantId_IsRecurring",
                table: "polar_report_products",
                columns: new[] { "TenantId", "IsRecurring" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_products_TenantId_PolarProductId",
                table: "polar_report_products",
                columns: new[] { "TenantId", "PolarProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "polar_report_benefits");

            migrationBuilder.DropTable(
                name: "polar_report_checkout_links");

            migrationBuilder.DropTable(
                name: "polar_report_customer_meters");

            migrationBuilder.DropTable(
                name: "polar_report_discounts");

            migrationBuilder.DropTable(
                name: "polar_report_license_keys");

            migrationBuilder.DropTable(
                name: "polar_report_meters");

            migrationBuilder.DropTable(
                name: "polar_report_products");
        }
    }
}
