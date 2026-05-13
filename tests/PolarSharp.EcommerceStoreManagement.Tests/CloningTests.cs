using PolarSharp.EcommerceStoreManagement.Cloning;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Verifies the cloning service public shapes — overrides records, options defaults, error
/// discriminator. End-to-end DB tests for the EF impls require a fully-wired Finbuckle
/// tenant context and live under <c>Category=Integration</c> against the per-provider
/// integration suites.
/// </summary>
public sealed class CloningContractTests
{
    [Fact]
    public void CloneProductOptions_default_includes_everything()
    {
        var opts = new CloneProductOptions();
        Assert.True(opts.IncludeVariants);
        Assert.True(opts.IncludeCategoryAssignments);
        Assert.True(opts.IncludeTranslations);
        Assert.True(opts.IncludeAttachedBenefits);
    }

    [Fact]
    public void CloneCategoryOptions_default_includes_translations()
    {
        var opts = new CloneCategoryOptions();
        Assert.True(opts.IncludeTranslations);
    }

    [Fact]
    public void CloneBenefitOptions_default_includes_translations()
    {
        var opts = new CloneBenefitOptions();
        Assert.True(opts.IncludeTranslations);
    }

    [Fact]
    public void CloneDiscountOptions_default_includes_applicable_products()
    {
        var opts = new CloneDiscountOptions();
        Assert.True(opts.IncludeApplicableProducts);
    }

    [Fact]
    public void CloneCheckoutLinkOptions_default_includes_custom_fields()
    {
        var opts = new CloneCheckoutLinkOptions();
        Assert.True(opts.IncludeCustomFields);
    }

    [Fact]
    public void CloningErrorKind_covers_documented_failure_modes()
    {
        var kinds = Enum.GetValues<CloningErrorKind>();
        Assert.Contains(CloningErrorKind.SourceNotFound, kinds);
        Assert.Contains(CloningErrorKind.OverrideConflictsWithExistingRow, kinds);
        Assert.Contains(CloningErrorKind.NameCollisionExhausted, kinds);
        Assert.Contains(CloningErrorKind.PersistenceFailed, kinds);
    }

    [Fact]
    public void CloneDiscountOverrides_NewCode_default_is_null_for_safe_coupon_clone()
    {
        // The discount cloning service's documented behaviour: if the caller doesn't supply
        // an explicit NewCode, the clone's coupon code is set to null (becomes an automatic
        // discount) — avoiding the (tenant, code) unique-index collision.
        var overrides = new CloneDiscountOverrides();
        Assert.Null(overrides.NewCode);
    }

    [Fact]
    public void CloneProductOverrides_with_NewCategoryIds_empty_uncategorises()
    {
        var overrides = new CloneProductOverrides { NewCategoryIds = [] };
        Assert.NotNull(overrides.NewCategoryIds);
        Assert.Empty(overrides.NewCategoryIds);
    }
}

/// <summary>
/// Verifies <c>CopySuffix.NextAvailableAsync</c> generates collision-free names — the core
/// duplicate-prevention guarantee.
/// </summary>
public sealed class CopySuffixTests
{
    [Fact]
    public async Task First_attempt_returns_base_paren_Copy_when_unused()
    {
        var existing = new HashSet<string>();
        var result = await PickAsync("Premium T-Shirt", existing);
        Assert.Equal("Premium T-Shirt (Copy)", result);
    }

    [Fact]
    public async Task Falls_through_to_Copy_2_when_simple_Copy_exists()
    {
        var existing = new HashSet<string> { "Premium T-Shirt (Copy)" };
        var result = await PickAsync("Premium T-Shirt", existing);
        Assert.Equal("Premium T-Shirt (Copy 2)", result);
    }

    [Fact]
    public async Task Falls_through_consecutive_collisions()
    {
        var existing = new HashSet<string>
        {
            "Premium T-Shirt (Copy)",
            "Premium T-Shirt (Copy 2)",
            "Premium T-Shirt (Copy 3)",
            "Premium T-Shirt (Copy 4)",
        };
        var result = await PickAsync("Premium T-Shirt", existing);
        Assert.Equal("Premium T-Shirt (Copy 5)", result);
    }

    [Fact]
    public async Task Returns_null_when_exhausted_after_100_attempts()
    {
        var existing = new HashSet<string> { "x (Copy)" };
        for (var i = 2; i <= 101; i++) existing.Add($"x (Copy {i})");

        var result = await PickAsync("x", existing);
        Assert.Null(result);
    }

    private static Task<string?> PickAsync(string baseName, HashSet<string> existing)
    {
        // CopySuffix is internal — invoke via reflection so the test can stay outside the
        // EF base assembly.
        var copySuffixType = typeof(PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PolarCatalogDbContext)
            .Assembly
            .GetType("PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning.CopySuffix")!;
        var method = copySuffixType.GetMethod("NextAvailableAsync")!;
        Func<string, CancellationToken, Task<bool>> exists = (candidate, _) =>
            Task.FromResult(existing.Contains(candidate));
        return (Task<string?>)method.Invoke(null, [baseName, exists, CancellationToken.None])!;
    }
}
