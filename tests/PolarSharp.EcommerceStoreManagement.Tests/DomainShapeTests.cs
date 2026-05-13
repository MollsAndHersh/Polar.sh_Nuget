using PolarSharp.BaseEntities;
using PolarSharp.EcommerceStoreManagement;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Verifies the catalog domain records inherit from the BaseEntities bases (so the host's
/// types see Polar's webhook wire shape) and that the M:N category relationship works.
/// </summary>
public sealed class DomainShapeTests
{
    [Fact]
    public void LocalProduct_inherits_from_PolarProductBase()
    {
        // Compile-time proof that LocalProduct IS-A PolarProductBase — this is the host
        // inheritance philosophy: ONE shape for products across the whole stack.
        var product = NewProduct();
        Assert.IsAssignableFrom<PolarProductBase>(product);
        Assert.IsAssignableFrom<ITenantOwned>(product);
        Assert.IsAssignableFrom<IFakeDataAware>(product);
    }

    [Fact]
    public void LocalCategory_inherits_from_PolarCategoryBase()
    {
        var category = NewCategory();
        Assert.IsAssignableFrom<PolarCategoryBase>(category);
        Assert.IsAssignableFrom<ITenantOwned>(category);
    }

    [Fact]
    public void LocalBenefit_subtypes_all_inherit_LocalBenefit_and_PolarBenefitBase()
    {
        LocalBenefit lk = NewLicenseKeysBenefit();
        Assert.IsAssignableFrom<PolarBenefitBase>(lk);
        Assert.IsType<LicenseKeysBenefit>(lk);
    }

    [Fact]
    public void Product_can_be_assigned_to_multiple_categories_via_M_to_N_list()
    {
        var c1 = NewCategory();
        var c2 = NewCategory();
        var c3 = NewCategory();

        var product = NewProduct() with
        {
            CategoryIds = [new CategoryId(Guid.NewGuid()), new CategoryId(Guid.NewGuid()), new CategoryId(Guid.NewGuid())],
        };

        Assert.Equal(3, product.CategoryIds.Count);
        Assert.NotEqual(product.CategoryIds[0], product.CategoryIds[1]);
    }

    [Fact]
    public void TenantBusinessProfile_inherits_PolarTenantBase_and_explicit_ITenantOwned_projects_Id()
    {
        var profile = new TenantBusinessProfile
        {
            Id = "01234567-89ab-cdef-0123-456789abcdef",
            Name = "Acme",
            Slug = "acme",
            CreatedAt = DateTimeOffset.UtcNow,
            Country = "US",
            DefaultPresentmentCurrency = "USD",
        };

        Assert.IsAssignableFrom<PolarTenantBase>(profile);
        ITenantOwned scoped = profile;
        Assert.Equal(profile.Id, scoped.TenantId);
    }

    [Fact]
    public void LocalProductVariant_IsOutOfStock_is_true_only_when_count_is_zero_or_negative()
    {
        var inStock = new LocalProductVariant { Id = VariantId.New(), Axes = new Dictionary<string, string>(), InventoryCount = 5 };
        var out_ = new LocalProductVariant { Id = VariantId.New(), Axes = new Dictionary<string, string>(), InventoryCount = 0 };
        var untracked = new LocalProductVariant { Id = VariantId.New(), Axes = new Dictionary<string, string>(), InventoryCount = null };

        Assert.False(inStock.IsOutOfStock);
        Assert.True(out_.IsOutOfStock);
        Assert.False(untracked.IsOutOfStock);
    }

    [Fact]
    public void LocalProductVariant_IsLowStock_requires_both_count_and_threshold_present()
    {
        var lowWithThreshold = new LocalProductVariant
        {
            Id = VariantId.New(),
            Axes = new Dictionary<string, string>(),
            InventoryCount = 3,
            InventoryLowThreshold = 5,
        };
        var aboveThreshold = lowWithThreshold with { InventoryCount = 100 };
        var noThreshold = new LocalProductVariant { Id = VariantId.New(), Axes = new Dictionary<string, string>(), InventoryCount = 3 };

        Assert.True(lowWithThreshold.IsLowStock);
        Assert.False(aboveThreshold.IsLowStock);
        Assert.False(noThreshold.IsLowStock);   // no threshold = never "low"
    }

    private static LocalProduct NewProduct() => new()
    {
        Id = Guid.NewGuid().ToString(),
        TenantId = "tenant-x",
        Name = "Master Product",
        OrganizationId = "org_test",
        CreatedAt = DateTimeOffset.UtcNow,
        MasterName = "Master Product",
        MasterLanguage = "en-US",
        Kind = ProductKind.Product,
        Price = new LocalPrice { Kind = PriceKind.Fixed, Amount = 1000, Currency = "USD" },
    };

    private static LocalCategory NewCategory() => new()
    {
        Id = Guid.NewGuid().ToString(),
        TenantId = "tenant-x",
        Name = "Cat",
        MasterName = "Cat",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static LicenseKeysBenefit NewLicenseKeysBenefit() => new()
    {
        Id = Guid.NewGuid().ToString(),
        BenefitId = BenefitId.New(),
        TenantId = "tenant-x",
        Type = PolarBenefitType.LicenseKeys,
        Description = "Auto-issued license keys",
        OrganizationId = "org_test",
        CreatedAt = DateTimeOffset.UtcNow,
        Name = "Pro License",
        KeyPrefix = "ACME-",
        Format = LicenseKeyFormat.Uuid,
    };
}
