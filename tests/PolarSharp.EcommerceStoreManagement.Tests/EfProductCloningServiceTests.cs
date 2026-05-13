using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using PolarSharp.EcommerceStoreManagement.Translation;
using static PolarSharp.EcommerceStoreManagement.Tests.Infrastructure.ResultTestExtensions;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Retroactive coverage for <c>EfProductCloningService</c> behavioral guarantees that were
/// previously untested — name auto-suffix on collision, caller-supplied name collision,
/// Polar-side state reset, variant/category/translation cascade toggling, and cross-tenant
/// isolation. Uses the shared <see cref="CatalogTestContext"/> harness for the in-memory
/// SQLite catalog DbContext + faked Finbuckle tenant accessor.
/// </summary>
public sealed class EfProductCloningServiceTests
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    [Fact]
    public async Task Clone_auto_suffixes_name_on_collision_with_existing_product()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: TenantA,
            configureServices: s => s.AddPolarCatalogCloning());

        var source = await SeedProductAsync(ctx, name: "Premium T-Shirt");

        var result = await CloneAsync(ctx, source);

        Assert.True(result.IsSuccess);
        Assert.Equal("Premium T-Shirt (Copy)", result.ValueOrThrow().MasterName);
    }

    [Fact]
    public async Task Clone_twice_yields_distinct_Copy_2_suffix()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: TenantA,
            configureServices: s => s.AddPolarCatalogCloning());

        var source = await SeedProductAsync(ctx, name: "Premium T-Shirt");

        var first = await CloneAsync(ctx, source);
        Assert.True(first.IsSuccess);
        Assert.Equal("Premium T-Shirt (Copy)", first.ValueOrThrow().MasterName);

        var second = await CloneAsync(ctx, source);
        Assert.True(second.IsSuccess);
        Assert.Equal("Premium T-Shirt (Copy 2)", second.ValueOrThrow().MasterName);
    }

    [Fact]
    public async Task Clone_with_caller_supplied_name_that_collides_returns_OverrideConflict()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: TenantA,
            configureServices: s => s.AddPolarCatalogCloning());

        var source = await SeedProductAsync(ctx, name: "Premium T-Shirt");
        await SeedProductAsync(ctx, name: "Already Taken");

        var result = await CloneAsync(ctx, source, overrides: new CloneProductOverrides
        {
            NewMasterName = "Already Taken",
        });

        Assert.True(result.IsFailure);
        Assert.Equal(CloningErrorKind.OverrideConflictsWithExistingRow, result.ErrorOrThrow().Kind);
    }

    [Fact]
    public async Task Clone_resets_Polar_side_state_to_fresh_Draft()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: TenantA,
            configureServices: s => s.AddPolarCatalogCloning());

        // Seed a previously-published product with Polar ids set.
        var sourceId = await SeedProductAsync(
            ctx,
            name: "Premium T-Shirt",
            polarProductId: "polar_prod_abc123",
            status: PublishStatus.Published,
            lastPublishedAt: DateTimeOffset.UtcNow);

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ValueOrThrow().PolarProductId);
        Assert.Null(result.ValueOrThrow().LastPublishedAt);
        Assert.Equal(PublishStatus.Draft, result.ValueOrThrow().Status);
    }

    [Fact]
    public async Task Clone_with_IncludeVariants_true_duplicates_variants_with_fresh_ids()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: TenantA,
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedProductAsync(ctx, name: "Premium T-Shirt", hasVariants: true);
        await SeedVariantsAsync(ctx, sourceId.Value, count: 3);

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var newId = Guid.Parse(result.ValueOrThrow().Id);
        var newVariants = await db.Variants.Where(v => v.ProductId == newId).ToListAsync();
        Assert.Equal(3, newVariants.Count);
        // Fresh ids — none match the source's variant ids.
        var sourceVariantIds = await db.Variants.Where(v => v.ProductId == sourceId.Value).Select(v => v.Id).ToListAsync();
        Assert.Empty(newVariants.Select(v => v.Id).Intersect(sourceVariantIds));
    }

    [Fact]
    public async Task Clone_with_IncludeVariants_false_skips_the_variant_cascade()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: TenantA,
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedProductAsync(ctx, name: "Premium T-Shirt", hasVariants: true);
        await SeedVariantsAsync(ctx, sourceId.Value, count: 3);

        var result = await CloneAsync(ctx, sourceId, options: new CloneProductOptions { IncludeVariants = false });

        Assert.True(result.IsSuccess);
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var newId = Guid.Parse(result.ValueOrThrow().Id);
        var newVariants = await db.Variants.Where(v => v.ProductId == newId).ToListAsync();
        Assert.Empty(newVariants);
    }

    [Fact]
    public async Task Clone_with_IncludeTranslations_true_duplicates_translation_rows()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: TenantA,
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedProductAsync(ctx, name: "Premium T-Shirt");
        await SeedTranslationsAsync(ctx, sourceId.Value, languages: ["es-MX", "fr-FR"]);

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var newId = Guid.Parse(result.ValueOrThrow().Id);
        var translations = await db.Translations
            .Where(t => t.EntityType == CatalogTranslationEntityType.Product && t.EntityId == newId)
            .ToListAsync();
        Assert.Equal(2, translations.Count);
        Assert.Contains(translations, t => t.Language == "es-MX");
        Assert.Contains(translations, t => t.Language == "fr-FR");
    }

    [Fact]
    public async Task Clone_with_IncludeTranslations_false_skips_translation_cascade()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: TenantA,
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedProductAsync(ctx, name: "Premium T-Shirt");
        await SeedTranslationsAsync(ctx, sourceId.Value, languages: ["es-MX", "fr-FR"]);

        var result = await CloneAsync(ctx, sourceId, options: new CloneProductOptions { IncludeTranslations = false });

        Assert.True(result.IsSuccess);
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var newId = Guid.Parse(result.ValueOrThrow().Id);
        var translations = await db.Translations
            .Where(t => t.EntityType == CatalogTranslationEntityType.Product && t.EntityId == newId)
            .ToListAsync();
        Assert.Empty(translations);
    }

    [Fact]
    public async Task Clone_with_IncludeCategoryAssignments_true_duplicates_MtoN_rows()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: TenantA,
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedProductAsync(ctx, name: "Premium T-Shirt");
        var category1Id = await SeedCategoryAsync(ctx, name: "Audio");
        var category2Id = await SeedCategoryAsync(ctx, name: "Mobile Accessories");
        await AssignCategoryAsync(ctx, sourceId.Value, category1Id);
        await AssignCategoryAsync(ctx, sourceId.Value, category2Id);

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var newId = Guid.Parse(result.ValueOrThrow().Id);
        var assignments = await db.ProductCategories.Where(a => a.ProductId == newId).ToListAsync();
        Assert.Equal(2, assignments.Count);
        Assert.Contains(assignments, a => a.CategoryId == category1Id);
        Assert.Contains(assignments, a => a.CategoryId == category2Id);
    }

    [Fact]
    public async Task Clone_cross_tenant_returns_SourceNotFound_when_source_belongs_to_another_tenant()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: TenantA,
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedProductAsync(ctx, name: "Tenant A Product");

        // Switch to a different tenant — the source product should be invisible.
        ctx.SetCurrentTenant(TenantB);
        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsFailure);
        Assert.Equal(CloningErrorKind.SourceNotFound, result.ErrorOrThrow().Kind);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<ProductId> SeedProductAsync(
        CatalogTestContext ctx,
        string name,
        bool hasVariants = false,
        string? polarProductId = null,
        PublishStatus status = PublishStatus.Draft,
        DateTimeOffset? lastPublishedAt = null)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.Products.Add(new LocalProductEntity
        {
            Id = id,
            MasterName = name,
            MasterDescription = name + " — master description",
            MasterLanguage = "en-US",
            Kind = ProductKind.Product,
            HasVariants = hasVariants,
            PriceJson = """{"Kind":3,"Currency":"USD","IsRecurring":false}""",
            AttachedBenefitsJson = "[]",
            PolarProductId = polarProductId,
            Status = status,
            LastPublishedAt = lastPublishedAt,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return new ProductId(id);
    }

    private static async Task SeedVariantsAsync(CatalogTestContext ctx, Guid productId, int count)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        for (var i = 0; i < count; i++)
        {
            db.Variants.Add(new LocalProductVariantEntity
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                AxesJson = $$"""{"size":"{{i}}"}""",
                IsActive = true,
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedTranslationsAsync(CatalogTestContext ctx, Guid productId, IReadOnlyList<string> languages)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        foreach (var lang in languages)
        {
            db.Translations.Add(new CatalogTranslationEntity
            {
                Id = Guid.NewGuid(),
                EntityType = CatalogTranslationEntityType.Product,
                EntityId = productId,
                Language = lang,
                FieldName = "name",
                TranslatedValue = $"Translated to {lang}",
                IsMachineTranslated = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedCategoryAsync(CatalogTestContext ctx, string name)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.Categories.Add(new LocalCategoryEntity
        {
            Id = id,
            MasterName = name,
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task AssignCategoryAsync(CatalogTestContext ctx, Guid productId, Guid categoryId)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        db.ProductCategories.Add(new LocalProductCategoryAssignmentEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            CategoryId = categoryId,
            AssignedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Result<LocalProduct, CloningError>> CloneAsync(
        CatalogTestContext ctx,
        ProductId source,
        CloneProductOverrides? overrides = null,
        CloneProductOptions? options = null)
    {
        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IProductCloningService>();
        return await svc.CloneAsync(source, overrides, options);
    }
}
