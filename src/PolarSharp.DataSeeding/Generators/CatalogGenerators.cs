using Bogus;
using PolarSharp.BaseEntities;
using PolarSharp.EcommerceStoreManagement;

namespace PolarSharp.DataSeeding.Generators;

/// <summary>
/// Produces fake <see cref="LocalProduct"/> records. Generated products have realistic names
/// (via Bogus's <c>Commerce</c> dataset), a master description, a fixed-price <see cref="LocalPrice"/>,
/// and <c>IsFakeData = true</c>.
/// </summary>
public sealed class FakeProductGenerator : IFakeGenerator<LocalProduct>
{
    /// <inheritdoc/>
    public LocalProduct Generate(string tenantId, Faker faker)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(faker);

        var name = faker.Commerce.ProductName();
        var amountMinor = (int)(faker.Random.Decimal(5m, 500m) * 100m);
        var currency = faker.PickRandom("USD", "EUR", "GBP");
        var now = faker.Date.RecentOffset(days: 90).ToUniversalTime();

        return new LocalProduct
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            OrganizationId = tenantId,
            Name = name,
            MasterName = name,
            MasterDescription = faker.Commerce.ProductDescription(),
            MasterLanguage = "en-US",
            Kind = ProductKind.Product,
            Price = new LocalPrice
            {
                Kind = PriceKind.Fixed,
                Amount = amountMinor,
                Currency = currency,
            },
            Manufacturer = faker.Company.CompanyName(),
            CreatedAt = now,
            ModifiedAt = null,
            IsArchived = false,
            IsRecurring = false,
            Status = PublishStatus.Draft,
            IsFakeData = true,
        };
    }
}

/// <summary>Produces fake <see cref="LocalCategory"/> records — names sourced from Bogus's commerce vocabulary.</summary>
public sealed class FakeCategoryGenerator : IFakeGenerator<LocalCategory>
{
    /// <inheritdoc/>
    public LocalCategory Generate(string tenantId, Faker faker)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(faker);
        var name = faker.Commerce.Categories(1)[0];
        var now = faker.Date.RecentOffset(days: 180).ToUniversalTime();
        return new LocalCategory
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Name = name,
            MasterName = name,
            Description = faker.Lorem.Sentence(),
            CreatedAt = now,
            IsFakeData = true,
        };
    }
}

/// <summary>Produces fake <see cref="LocalDepartment"/> records.</summary>
public sealed class FakeDepartmentGenerator : IFakeGenerator<LocalDepartment>
{
    /// <inheritdoc/>
    public LocalDepartment Generate(string tenantId, Faker faker)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(faker);
        var name = faker.PickRandom("Apparel", "Electronics", "Home", "Garden", "Sports", "Books", "Toys", "Beauty", "Grocery", "Software");
        var now = faker.Date.RecentOffset(days: 365).ToUniversalTime();
        return new LocalDepartment
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Name = name,
            MasterName = name,
            Description = $"{name} department",
            SortOrder = faker.Random.Int(0, 100),
            CreatedAt = now,
            IsFakeData = true,
        };
    }
}

/// <summary>Produces fake <see cref="LocalDiscount"/> records (mix of automatic + coupon-code discounts).</summary>
public sealed class FakeDiscountGenerator : IFakeGenerator<LocalDiscount>
{
    /// <inheritdoc/>
    public LocalDiscount Generate(string tenantId, Faker faker)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(faker);

        var isPercent = faker.Random.Bool();
        var hasCode = faker.Random.Bool();
        var name = faker.Commerce.ProductAdjective() + " " + faker.PickRandom("Sale", "Promo", "Special", "Deal");
        var now = faker.Date.RecentOffset(days: 30).ToUniversalTime();

        return new LocalDiscount
        {
            Id = Guid.NewGuid().ToString(),
            DiscountId = DiscountId.New(),
            TenantId = tenantId,
            Name = name,
            MasterName = name,
            Code = hasCode ? faker.Hashids.Encode(faker.Random.Number(1000, 9999)).ToUpperInvariant() : null,
            Kind = isPercent ? DiscountKind.Percentage : DiscountKind.Fixed,
            Type = isPercent ? "percentage" : "fixed",
            PercentageOff = isPercent ? faker.Random.Decimal(5, 50) : null,
            AmountOff = !isPercent ? faker.Random.Int(100, 5000) : null,
            Currency = isPercent ? null : "USD",
            OrganizationId = tenantId,
            CreatedAt = now,
            Status = PublishStatus.Draft,
            IsFakeData = true,
        };
    }
}

/// <summary>Produces fake <see cref="LicenseKeysBenefit"/> records — represents a "Pro license" / "Lifetime license" kind of benefit.</summary>
public sealed class FakeLicenseKeysBenefitGenerator : IFakeGenerator<LicenseKeysBenefit>
{
    /// <inheritdoc/>
    public LicenseKeysBenefit Generate(string tenantId, Faker faker)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(faker);

        var name = faker.PickRandom("Pro License", "Lifetime License", "Standard License", "Enterprise License", "Premium License");
        var now = faker.Date.RecentOffset(days: 180).ToUniversalTime();

        return new LicenseKeysBenefit
        {
            Id = Guid.NewGuid().ToString(),
            BenefitId = BenefitId.New(),
            TenantId = tenantId,
            Type = PolarBenefitType.LicenseKeys,
            Description = faker.Lorem.Sentence(),
            OrganizationId = tenantId,
            CreatedAt = now,
            Name = name,
            KeyPrefix = faker.Random.AlphaNumeric(4).ToUpperInvariant() + "-",
            MaxActivations = faker.Random.Bool() ? faker.Random.Int(1, 10) : null,
            Format = LicenseKeyFormat.Uuid,
            Status = PublishStatus.Draft,
            IsFakeData = true,
        };
    }
}

/// <summary>Produces fake <see cref="LocalCheckoutLinkConfig"/> records.</summary>
public sealed class FakeCheckoutLinkGenerator : IFakeGenerator<LocalCheckoutLinkConfig>
{
    /// <inheritdoc/>
    public LocalCheckoutLinkConfig Generate(string tenantId, Faker faker)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(faker);
        var now = faker.Date.RecentOffset(days: 30).ToUniversalTime();
        return new LocalCheckoutLinkConfig
        {
            Id = CheckoutLinkId.New(),
            TenantId = tenantId,
            Name = $"Checkout: {faker.Commerce.ProductAdjective()} {faker.Commerce.Department(1)}",
            ProductIds = [],   // host wires real product ids when calling SeedAsync after products exist
            SuccessUrl = "https://example.com/checkout/success",
            CancelUrl = "https://example.com/checkout/cancel",
            ThemeColor = faker.Internet.Color(),
            AllowDiscountCodes = true,
            Status = PublishStatus.Draft,
            IsFakeData = true,
        };
    }
}
