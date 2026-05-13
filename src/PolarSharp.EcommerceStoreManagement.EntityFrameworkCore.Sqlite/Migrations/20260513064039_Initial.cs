using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_translations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TranslatedValue = table.Column<string>(type: "TEXT", nullable: false),
                    IsMachineTranslated = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceProvider = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    SourceModel = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_translations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_admin_audit_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    BeforeValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedFields = table.Column<string>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false),
                    CrossTenantAccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    CrossTenantJustification = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_admin_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_business_profiles",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OrganizationName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    TaxBehavior = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StreetLine1 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    StreetLine2 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    StateOrProvince = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ProductDescription = table.Column<string>(type: "TEXT", nullable: true),
                    IntendedUse = table.Column<string>(type: "TEXT", nullable: true),
                    PricingModelsJson = table.Column<string>(type: "TEXT", nullable: true),
                    SellingCategoriesJson = table.Column<string>(type: "TEXT", nullable: true),
                    FutureAnnualRevenue = table.Column<long>(type: "INTEGER", nullable: true),
                    SwitchingFrom = table.Column<string>(type: "TEXT", nullable: true),
                    LegalEntityJson = table.Column<string>(type: "TEXT", nullable: true),
                    StripeConnectAccountId = table.Column<string>(type: "TEXT", nullable: true),
                    PayoutAccountId = table.Column<string>(type: "TEXT", nullable: true),
                    PayoutStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PayoutStatusLastCheckedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TranslationProvider = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TranslationApiKeyEncrypted = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationModel = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEndpoint = table.Column<string>(type: "TEXT", nullable: true),
                    MasterLanguage = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SupportedLanguagesJson = table.Column<string>(type: "TEXT", nullable: false),
                    AutoTranslateOnSave = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowFakeData = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_business_profiles", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_benefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    BenefitKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    PropertiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    PolarBenefitId = table.Column<string>(type: "TEXT", nullable: true),
                    LastPublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_benefits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    MasterName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_checkout_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProductIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SuccessUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CancelUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ThemeColor = table.Column<string>(type: "TEXT", nullable: true),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "TEXT", nullable: false),
                    AllowDiscountCodes = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequireBillingAddress = table.Column<bool>(type: "INTEGER", nullable: false),
                    PolarCheckoutLinkId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_checkout_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    MasterName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_discounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    MasterName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AmountOff = table.Column<int>(type: "INTEGER", nullable: true),
                    PercentageOff = table.Column<decimal>(type: "TEXT", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", nullable: true),
                    DurationWire = table.Column<string>(type: "TEXT", nullable: true),
                    DurationKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    DurationInMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "INTEGER", nullable: true),
                    ApplicableProductIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PolarDiscountId = table.Column<string>(type: "TEXT", nullable: true),
                    LastPublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_discounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_product_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_product_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    MasterName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    MasterDescription = table.Column<string>(type: "TEXT", nullable: true),
                    MasterLanguage = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TierGroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    HasVariants = table.Column<bool>(type: "INTEGER", nullable: false),
                    PriceJson = table.Column<string>(type: "TEXT", nullable: false),
                    AttachedBenefitsJson = table.Column<string>(type: "TEXT", nullable: false),
                    MsrpAmount = table.Column<int>(type: "INTEGER", nullable: true),
                    MsrpCurrency = table.Column<string>(type: "TEXT", nullable: true),
                    Manufacturer = table.Column<string>(type: "TEXT", nullable: true),
                    Isbn = table.Column<string>(type: "TEXT", nullable: true),
                    PolarProductId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LastPublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_tier_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LevelsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_tier_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AxesJson = table.Column<string>(type: "TEXT", nullable: false),
                    SurchargeAmount = table.Column<int>(type: "INTEGER", nullable: true),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PolarProductId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LastPublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    InventoryCount = table.Column<int>(type: "INTEGER", nullable: true),
                    InventoryLowThreshold = table.Column<int>(type: "INTEGER", nullable: true),
                    LastStockChangedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsFakeData = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_variants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_translations_TenantId_EntityType_EntityId",
                table: "catalog_translations",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_translations_TenantId_EntityType_EntityId_Language_FieldName",
                table: "catalog_translations",
                columns: new[] { "TenantId", "EntityType", "EntityId", "Language", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_admin_audit_log_TenantId_EntityType_EntityId",
                table: "polar_admin_audit_log",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_admin_audit_log_TenantId_OccurredAt",
                table: "polar_admin_audit_log",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_benefits_TenantId_BenefitKind",
                table: "polar_local_benefits",
                columns: new[] { "TenantId", "BenefitKind" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_categories_TenantId_ParentCategoryId",
                table: "polar_local_categories",
                columns: new[] { "TenantId", "ParentCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_departments_TenantId_MasterName",
                table: "polar_local_departments",
                columns: new[] { "TenantId", "MasterName" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_discounts_TenantId_Code",
                table: "polar_local_discounts",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_product_categories_ProductId_CategoryId",
                table: "polar_local_product_categories",
                columns: new[] { "ProductId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_product_categories_TenantId_CategoryId",
                table: "polar_local_product_categories",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_product_categories_TenantId_ProductId",
                table: "polar_local_product_categories",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_products_TenantId_IsFakeData",
                table: "polar_local_products",
                columns: new[] { "TenantId", "IsFakeData" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_products_TenantId_MasterName",
                table: "polar_local_products",
                columns: new[] { "TenantId", "MasterName" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_products_TenantId_PolarProductId",
                table: "polar_local_products",
                columns: new[] { "TenantId", "PolarProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_variants_TenantId_ProductId",
                table: "polar_local_variants",
                columns: new[] { "TenantId", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_translations");

            migrationBuilder.DropTable(
                name: "polar_admin_audit_log");

            migrationBuilder.DropTable(
                name: "polar_business_profiles");

            migrationBuilder.DropTable(
                name: "polar_local_benefits");

            migrationBuilder.DropTable(
                name: "polar_local_categories");

            migrationBuilder.DropTable(
                name: "polar_local_checkout_links");

            migrationBuilder.DropTable(
                name: "polar_local_departments");

            migrationBuilder.DropTable(
                name: "polar_local_discounts");

            migrationBuilder.DropTable(
                name: "polar_local_product_categories");

            migrationBuilder.DropTable(
                name: "polar_local_products");

            migrationBuilder.DropTable(
                name: "polar_local_tier_groups");

            migrationBuilder.DropTable(
                name: "polar_local_variants");
        }
    }
}
