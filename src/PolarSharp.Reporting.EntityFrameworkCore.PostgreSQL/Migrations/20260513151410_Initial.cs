using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "polar_report_benefit_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    PolarGrantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    BenefitId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BenefitName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BenefitKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsGranted = table.Column<bool>(type: "boolean", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_benefit_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    PolarCustomerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OrderCount = table.Column<int>(type: "integer", nullable: false),
                    LifetimeValue = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    FirstOrderAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastOrderAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    PolarEventId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_order_line_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PriceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitAmount = table.Column<long>(type: "bigint", nullable: false),
                    LineTotal = table.Column<long>(type: "bigint", nullable: false),
                    DiscountAmount = table.Column<long>(type: "bigint", nullable: false),
                    TaxAmount = table.Column<long>(type: "bigint", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_order_line_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_order_refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolarRefundId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_order_refunds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    PolarOrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    TaxAmount = table.Column<long>(type: "bigint", nullable: false),
                    RefundedAmount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    LineItemCount = table.Column<int>(type: "integer", nullable: false),
                    InvoiceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FulfilledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_report_snapshot_checkpoints",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Resource = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastPolarId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_snapshot_checkpoints", x => new { x.TenantId, x.Resource });
                });

            migrationBuilder.CreateTable(
                name: "polar_report_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    PolarSubscriptionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanceledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_report_subscriptions", x => x.Id);
                });

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
                name: "IX_polar_report_events_TenantId_OccurredAt",
                table: "polar_report_events",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_report_events_TenantId_PolarEventId",
                table: "polar_report_events",
                columns: new[] { "TenantId", "PolarEventId" },
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
                name: "polar_report_customers");

            migrationBuilder.DropTable(
                name: "polar_report_events");

            migrationBuilder.DropTable(
                name: "polar_report_order_line_items");

            migrationBuilder.DropTable(
                name: "polar_report_order_refunds");

            migrationBuilder.DropTable(
                name: "polar_report_orders");

            migrationBuilder.DropTable(
                name: "polar_report_snapshot_checkpoints");

            migrationBuilder.DropTable(
                name: "polar_report_subscriptions");
        }
    }
}
