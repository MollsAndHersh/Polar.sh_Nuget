using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;

/// <summary>EF entity for the tenant's business profile. One row per tenant.</summary>
public sealed class TenantBusinessProfileEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>The tenant id (PolarTenantBase.Id) — this entity's primary key.</summary>
    public string TenantId { get; set; } = "";
    /// <summary>Organization name.</summary>
    public string OrganizationName { get; set; } = "";
    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string CountryCode { get; set; } = "";
    /// <summary>ISO 4217 default presentment currency.</summary>
    public string DefaultCurrency { get; set; } = "";
    /// <summary>Tax behaviour.</summary>
    public DefaultTaxBehavior TaxBehavior { get; set; }
    /// <summary>Street line 1.</summary>
    public string? StreetLine1 { get; set; }
    /// <summary>Street line 2.</summary>
    public string? StreetLine2 { get; set; }
    /// <summary>City.</summary>
    public string? City { get; set; }
    /// <summary>State / province.</summary>
    public string? StateOrProvince { get; set; }
    /// <summary>Postal code.</summary>
    public string? PostalCode { get; set; }
    /// <summary>Product description.</summary>
    public string? ProductDescription { get; set; }
    /// <summary>Intended use.</summary>
    public string? IntendedUse { get; set; }
    /// <summary>JSON serialised pricing models list.</summary>
    public string? PricingModelsJson { get; set; }
    /// <summary>JSON serialised selling categories list.</summary>
    public string? SellingCategoriesJson { get; set; }
    /// <summary>Expected future annual revenue.</summary>
    public long? FutureAnnualRevenue { get; set; }
    /// <summary>Platform the tenant is migrating from.</summary>
    public string? SwitchingFrom { get; set; }
    /// <summary>Legal entity JSON pass-through.</summary>
    public string? LegalEntityJson { get; set; }
    /// <summary>Stripe Connect account id mirror.</summary>
    public string? StripeConnectAccountId { get; set; }
    /// <summary>Payout account id mirror.</summary>
    public string? PayoutAccountId { get; set; }
    /// <summary>Payout setup status.</summary>
    public PayoutSetupStatus PayoutStatus { get; set; }
    /// <summary>UTC of the last payout status poll.</summary>
    public DateTimeOffset? PayoutStatusLastCheckedAt { get; set; }
    /// <summary>Per-tenant translation provider.</summary>
    public TranslationProvider TranslationProvider { get; set; }
    /// <summary>Encrypted API key.</summary>
    public string? TranslationApiKeyEncrypted { get; set; }
    /// <summary>Translation model name.</summary>
    public string? TranslationModel { get; set; }
    /// <summary>Translation endpoint URL.</summary>
    public string? TranslationEndpoint { get; set; }
    /// <summary>Master language for catalog authoring.</summary>
    public string MasterLanguage { get; set; } = "en-US";
    /// <summary>JSON list of supported languages.</summary>
    public string SupportedLanguagesJson { get; set; } = """["en-US"]""";
    /// <summary>When true, every product save triggers an automatic translation pass.</summary>
    public bool AutoTranslateOnSave { get; set; }
    /// <summary>When false, the dual query filter hides fake-data rows.</summary>
    public bool AllowFakeData { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>EF entity for <see cref="LocalProduct"/>.</summary>
public sealed class LocalProductEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Local product id.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Master-language product name.</summary>
    public string MasterName { get; set; } = "";
    /// <summary>Master-language description.</summary>
    public string? MasterDescription { get; set; }
    /// <summary>Master language code.</summary>
    public string MasterLanguage { get; set; } = "en-US";
    /// <summary>Product vs Service.</summary>
    public ProductKind Kind { get; set; }
    /// <summary>Optional tier-group membership.</summary>
    public Guid? TierGroupId { get; set; }
    /// <summary>True when the product splays into multiple Polar Products at publish.</summary>
    public bool HasVariants { get; set; }
    /// <summary>JSON-serialized <see cref="LocalPrice"/>.</summary>
    public string PriceJson { get; set; } = "{}";
    /// <summary>JSON-serialized <see cref="BenefitId"/> list.</summary>
    public string AttachedBenefitsJson { get; set; } = "[]";
    /// <summary>MSRP amount (minor units).</summary>
    public int? MsrpAmount { get; set; }
    /// <summary>MSRP currency code.</summary>
    public string? MsrpCurrency { get; set; }
    /// <summary>Manufacturer.</summary>
    public string? Manufacturer { get; set; }
    /// <summary>ISBN.</summary>
    public string? Isbn { get; set; }
    /// <summary>Polar product id assigned at publish time.</summary>
    public string? PolarProductId { get; set; }
    /// <summary>UTC of last successful publish.</summary>
    public DateTimeOffset? LastPublishedAt { get; set; }
    /// <summary>Current publish status.</summary>
    public PublishStatus Status { get; set; }
    /// <summary>UTC of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>UTC of last modification.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>M:N join between products and categories. Implements <c>ITenantOwned</c> so the global filter scopes assignments per tenant.</summary>
public sealed class LocalProductCategoryAssignmentEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Product side of the M:N.</summary>
    public Guid ProductId { get; set; }
    /// <summary>Category side of the M:N.</summary>
    public Guid CategoryId { get; set; }
    /// <summary>UTC of the assignment.</summary>
    public DateTimeOffset AssignedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>EF entity for <see cref="LocalProductVariant"/>.</summary>
public sealed class LocalProductVariantEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Variant id.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Parent product id.</summary>
    public Guid ProductId { get; set; }
    /// <summary>Axes JSON ({"color":"red","size":"M"}).</summary>
    public string AxesJson { get; set; } = "{}";
    /// <summary>Surcharge amount in minor units.</summary>
    public int? SurchargeAmount { get; set; }
    /// <summary>SKU code.</summary>
    public string? Sku { get; set; }
    /// <summary>Polar product id for this variant.</summary>
    public string? PolarProductId { get; set; }
    /// <summary>UTC of last successful publish.</summary>
    public DateTimeOffset? LastPublishedAt { get; set; }
    /// <summary>Inventory active flag.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Current on-hand count. Null = not tracked.</summary>
    public int? InventoryCount { get; set; }
    /// <summary>Low-stock threshold.</summary>
    public int? InventoryLowThreshold { get; set; }
    /// <summary>UTC of last stock change.</summary>
    public DateTimeOffset? LastStockChangedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>EF entity for <see cref="LocalCategory"/>.</summary>
public sealed class LocalCategoryEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Category id.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Master-language category name.</summary>
    public string MasterName { get; set; } = "";
    /// <summary>Optional parent category.</summary>
    public Guid? ParentCategoryId { get; set; }
    /// <summary>Optional department.</summary>
    public Guid? DepartmentId { get; set; }
    /// <summary>Display sort order.</summary>
    public int SortOrder { get; set; }
    /// <summary>Description.</summary>
    public string? Description { get; set; }
    /// <summary>UTC of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>EF entity for <see cref="LocalDepartment"/>.</summary>
public sealed class LocalDepartmentEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Department id.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Master-language department name.</summary>
    public string MasterName { get; set; } = "";
    /// <summary>Description.</summary>
    public string? Description { get; set; }
    /// <summary>Display sort order.</summary>
    public int SortOrder { get; set; }
    /// <summary>UTC of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>EF entity for <see cref="LocalTierGroup"/>.</summary>
public sealed class LocalTierGroupEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Tier group id.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>JSON-serialized list of <see cref="TierLevel"/>.</summary>
    public string LevelsJson { get; set; } = "[]";
    /// <summary>UTC of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>EF entity for the polymorphic <see cref="LocalBenefit"/> hierarchy. Discriminator is <see cref="BenefitKind"/>; the subtype-specific properties are JSON-serialized.</summary>
public sealed class LocalBenefitEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Benefit id.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar benefit type discriminator (custom / license_keys / downloadables / github_repository / discord / feature_flag / meter_credit).</summary>
    public string BenefitKind { get; set; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Description (inherited from <see cref="PolarSharp.BaseEntities.PolarBenefitBase"/>).</summary>
    public string Description { get; set; } = "";
    /// <summary>JSON blob with the subtype-specific fields.</summary>
    public string PropertiesJson { get; set; } = "{}";
    /// <summary>Polar benefit id after publish.</summary>
    public string? PolarBenefitId { get; set; }
    /// <summary>UTC of last successful publish.</summary>
    public DateTimeOffset? LastPublishedAt { get; set; }
    /// <summary>Current publish status.</summary>
    public PublishStatus Status { get; set; }
    /// <summary>UTC of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>EF entity for <see cref="LocalDiscount"/>.</summary>
public sealed class LocalDiscountEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Discount id.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Master-language display name.</summary>
    public string MasterName { get; set; } = "";
    /// <summary>Customer-facing name (inherited base wire value).</summary>
    public string Name { get; set; } = "";
    /// <summary>Customer coupon code. <see langword="null"/> for automatic discounts.</summary>
    public string? Code { get; set; }
    /// <summary>Percentage vs fixed.</summary>
    public DiscountKind Kind { get; set; }
    /// <summary>Polar wire-format Type string (<c>"percentage"</c> or <c>"fixed"</c>).</summary>
    public string Type { get; set; } = "";
    /// <summary>Amount-off in minor units (when Kind = Fixed).</summary>
    public int? AmountOff { get; set; }
    /// <summary>Percentage-off (when Kind = Percentage).</summary>
    public decimal? PercentageOff { get; set; }
    /// <summary>Currency (when Kind = Fixed).</summary>
    public string? Currency { get; set; }
    /// <summary>Duration ("forever" / "once" / "repeating").</summary>
    public string? DurationWire { get; set; }
    /// <summary>Strongly-typed duration projection.</summary>
    public DiscountDuration? DurationKind { get; set; }
    /// <summary>Duration in months when repeating.</summary>
    public int? DurationInMonths { get; set; }
    /// <summary>UTC the discount becomes active.</summary>
    public DateTimeOffset? StartsAt { get; set; }
    /// <summary>UTC the discount expires.</summary>
    public DateTimeOffset? EndsAt { get; set; }
    /// <summary>Max redemptions across all customers.</summary>
    public int? MaxRedemptions { get; set; }
    /// <summary>JSON-serialized list of applicable Polar product ids.</summary>
    public string ApplicableProductIdsJson { get; set; } = "[]";
    /// <summary>Polar discount id after publish.</summary>
    public string? PolarDiscountId { get; set; }
    /// <summary>UTC of last publish.</summary>
    public DateTimeOffset? LastPublishedAt { get; set; }
    /// <summary>Current publish status.</summary>
    public PublishStatus Status { get; set; }
    /// <summary>UTC of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>EF entity for <see cref="LocalCheckoutLinkConfig"/>.</summary>
public sealed class LocalCheckoutLinkEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Checkout link id.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Host-side display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>JSON list of product ids on this link.</summary>
    public string ProductIdsJson { get; set; } = "[]";
    /// <summary>HTTPS URL Polar redirects to on success.</summary>
    public string? SuccessUrl { get; set; }
    /// <summary>HTTPS URL Polar redirects to on cancel.</summary>
    public string? CancelUrl { get; set; }
    /// <summary>Hex theme colour.</summary>
    public string? ThemeColor { get; set; }
    /// <summary>Logo image URL.</summary>
    public string? LogoUrl { get; set; }
    /// <summary>JSON-serialized list of <c>CheckoutCustomField</c>.</summary>
    public string CustomFieldsJson { get; set; } = "[]";
    /// <summary>When true, discount codes can be applied.</summary>
    public bool AllowDiscountCodes { get; set; } = true;
    /// <summary>When true, billing address is required.</summary>
    public bool RequireBillingAddress { get; set; }
    /// <summary>Polar checkout-link id after publish.</summary>
    public string? PolarCheckoutLinkId { get; set; }
    /// <summary>Current publish status.</summary>
    public PublishStatus Status { get; set; }
    /// <summary>UTC of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}
