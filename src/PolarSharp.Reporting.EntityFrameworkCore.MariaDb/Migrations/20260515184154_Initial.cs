using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.Reporting.EntityFrameworkCore.MariaDb.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_benefit_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarGrantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    OrderId = table.Column<Guid>(type: "char(36)", nullable: true),
                    BenefitId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    BenefitName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    BenefitKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    IsGranted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_benefit_grants", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_benefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarBenefitId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_benefits", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_checkout_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarCheckoutLinkId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ProductIdsCsv = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false),
                    Url = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true),
                    SuccessUrl = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true),
                    AllowDiscountCodes = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_checkout_links", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_customer_meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarCustomerMeterId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    MeterId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ConsumedUnits = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: false),
                    CreditedUnits = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_customer_meters", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarCustomerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    OrderCount = table.Column<int>(type: "int", nullable: false),
                    LifetimeValue = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    FirstOrderAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LastOrderAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_customers", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_discounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarDiscountId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    Type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    AmountOff = table.Column<long>(type: "bigint", nullable: true),
                    PercentOff = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: true),
                    RedemptionsSoFar = table.Column<int>(type: "int", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "int", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_discounts", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarEventId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Type = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: true),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_events", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_license_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarLicenseKeyId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    BenefitId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    DisplayKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    LimitActivations = table.Column<int>(type: "int", nullable: true),
                    ActivationsUsed = table.Column<int>(type: "int", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_license_keys", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarMeterId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    AggregationKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_meters", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_order_line_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    OrderId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    PriceId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitAmount = table.Column<long>(type: "bigint", nullable: false),
                    LineTotal = table.Column<long>(type: "bigint", nullable: false),
                    DiscountAmount = table.Column<long>(type: "bigint", nullable: false),
                    TaxAmount = table.Column<long>(type: "bigint", nullable: false),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_order_line_items", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_order_refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    OrderId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PolarRefundId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    Reason = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_order_refunds", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarOrderId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    OrderNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    TaxAmount = table.Column<long>(type: "bigint", nullable: false),
                    RefundedAmount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    LineItemCount = table.Column<int>(type: "int", nullable: false),
                    InvoiceUrl = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    FulfilledAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_orders", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarProductId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true),
                    IsRecurring = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RecurringInterval = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_products", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_snapshot_checkpoints",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Resource = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    LastPolarId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_snapshot_checkpoints", x => new { x.TenantId, x.Resource });
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "polar_report_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PolarSubscriptionId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ProductId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    CanceledAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    IsFakeData = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_subscriptions", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_benefit_grants_TenantId_CustomerId_IsGranted",
                table: "polar_report_benefit_grants",
                columns: new[] { "TenantId", "CustomerId", "IsGranted" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_benefit_grants_TenantId_OrderId",
                table: "polar_report_benefit_grants",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_benefit_grants_TenantId_PolarGrantId",
                table: "polar_report_benefit_grants",
                columns: new[] { "TenantId", "PolarGrantId" },
                unique: true);

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
                name: "IX_polar_report_customers_TenantId_Email",
                table: "polar_report_customers",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_customers_TenantId_LastOrderAt",
                table: "polar_report_customers",
                columns: new[] { "TenantId", "LastOrderAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_customers_TenantId_PolarCustomerId",
                table: "polar_report_customers",
                columns: new[] { "TenantId", "PolarCustomerId" },
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
                name: "IX_polar_report_events_TenantId_OccurredAt",
                table: "polar_report_events",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_events_TenantId_PolarEventId",
                table: "polar_report_events",
                columns: new[] { "TenantId", "PolarEventId" },
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
                name: "IX_polar_report_order_line_items_TenantId_OrderId",
                table: "polar_report_order_line_items",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_order_refunds_TenantId_OrderId",
                table: "polar_report_order_refunds",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_order_refunds_TenantId_PolarRefundId",
                table: "polar_report_order_refunds",
                columns: new[] { "TenantId", "PolarRefundId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_orders_TenantId_CreatedAt",
                table: "polar_report_orders",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_orders_TenantId_CustomerId_CreatedAt",
                table: "polar_report_orders",
                columns: new[] { "TenantId", "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_orders_TenantId_PolarOrderId",
                table: "polar_report_orders",
                columns: new[] { "TenantId", "PolarOrderId" },
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

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_subscriptions_TenantId_PolarSubscriptionId",
                table: "polar_report_subscriptions",
                columns: new[] { "TenantId", "PolarSubscriptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_subscriptions_TenantId_Status_StartedAt",
                table: "polar_report_subscriptions",
                columns: new[] { "TenantId", "Status", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "polar_report_benefit_grants");

            migrationBuilder.DropTable(
                name: "polar_report_benefits");

            migrationBuilder.DropTable(
                name: "polar_report_checkout_links");

            migrationBuilder.DropTable(
                name: "polar_report_customer_meters");

            migrationBuilder.DropTable(
                name: "polar_report_customers");

            migrationBuilder.DropTable(
                name: "polar_report_discounts");

            migrationBuilder.DropTable(
                name: "polar_report_events");

            migrationBuilder.DropTable(
                name: "polar_report_license_keys");

            migrationBuilder.DropTable(
                name: "polar_report_meters");

            migrationBuilder.DropTable(
                name: "polar_report_order_line_items");

            migrationBuilder.DropTable(
                name: "polar_report_order_refunds");

            migrationBuilder.DropTable(
                name: "polar_report_orders");

            migrationBuilder.DropTable(
                name: "polar_report_products");

            migrationBuilder.DropTable(
                name: "polar_report_snapshot_checkpoints");

            migrationBuilder.DropTable(
                name: "polar_report_subscriptions");
        }
    }
}
