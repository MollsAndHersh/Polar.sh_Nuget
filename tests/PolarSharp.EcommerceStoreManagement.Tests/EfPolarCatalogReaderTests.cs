using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Reading;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Translation;
using PolarSharp.EcommerceStoreManagement.Reading;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Tests for the v1.3.B <see cref="EfPolarCatalogReader"/>. Covers per-field translation
/// merge with master-language fallback (the core localization promise), missing-entity
/// handling, and per-language isolation.
/// </summary>
public sealed class EfPolarCatalogReaderTests
{
    private static void Configure(IServiceCollection s)
    {
        s.AddMemoryCache();
        s.AddSingleton<IOptions<TranslationCacheOptions>>(Options.Create(new TranslationCacheOptions()));
        s.AddSingleton<IPolarCatalogTranslationCache, MemoryPolarCatalogTranslationCache>();
        s.AddScoped<ITranslationRepository, EfTranslationRepository>();
        s.AddScoped<IPolarCatalogReader, EfPolarCatalogReader>();
    }

    [Fact]
    public async Task GetProductLocalizedAsync_with_no_translations_returns_master_values()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var productId = await SeedProductAsync(ctx, masterName: "Premium T-Shirt", masterDescription: "The original T-shirt");

        using var scope = ctx.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPolarCatalogReader>();
        var product = await reader.GetProductLocalizedAsync(new ProductId(productId), "es-MX");

        Assert.NotNull(product);
        Assert.Equal("Premium T-Shirt", product.MasterName);
        Assert.Equal("Premium T-Shirt", product.Name);            // Wire-format Name follows master
        Assert.Equal("The original T-shirt", product.MasterDescription);
    }

    [Fact]
    public async Task GetProductLocalizedAsync_with_full_translations_returns_translated_values()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var productId = await SeedProductAsync(ctx, masterName: "Premium T-Shirt", masterDescription: "The original T-shirt");
        await SeedTranslationAsync(ctx, productId, "es-MX", "name", "Camiseta Premium");
        await SeedTranslationAsync(ctx, productId, "es-MX", "description", "La camiseta original");

        using var scope = ctx.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPolarCatalogReader>();
        var product = await reader.GetProductLocalizedAsync(new ProductId(productId), "es-MX");

        Assert.NotNull(product);
        Assert.Equal("Camiseta Premium", product.Name);
        Assert.Equal("La camiseta original", product.Description);
    }

    [Fact]
    public async Task GetProductLocalizedAsync_with_partial_translations_falls_back_per_field_to_master()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var productId = await SeedProductAsync(ctx, masterName: "Premium T-Shirt", masterDescription: "The original T-shirt");

        // Only name is translated; description should fall back to master.
        await SeedTranslationAsync(ctx, productId, "es-MX", "name", "Camiseta Premium");

        using var scope = ctx.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPolarCatalogReader>();
        var product = await reader.GetProductLocalizedAsync(new ProductId(productId), "es-MX");

        Assert.NotNull(product);
        Assert.Equal("Camiseta Premium", product.Name);                  // translated
        Assert.Equal("The original T-shirt", product.Description);       // master fallback
    }

    [Fact]
    public async Task GetProductLocalizedAsync_requesting_unsupported_language_falls_back_to_master_per_field()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var productId = await SeedProductAsync(ctx, masterName: "Premium T-Shirt", masterDescription: "The original T-shirt");

        // Translations exist for es-MX but request fr-FR — every field falls back.
        await SeedTranslationAsync(ctx, productId, "es-MX", "name", "Camiseta Premium");

        using var scope = ctx.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPolarCatalogReader>();
        var product = await reader.GetProductLocalizedAsync(new ProductId(productId), "fr-FR");

        Assert.NotNull(product);
        Assert.Equal("Premium T-Shirt", product.Name);                   // master
        Assert.Equal("The original T-shirt", product.Description);
    }

    [Fact]
    public async Task GetProductLocalizedAsync_with_missing_product_returns_null()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);

        using var scope = ctx.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPolarCatalogReader>();
        var product = await reader.GetProductLocalizedAsync(new ProductId(Guid.NewGuid()), "es-MX");

        Assert.Null(product);
    }

    [Fact]
    public async Task GetCategoryLocalizedAsync_merges_translation_into_master_name()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var categoryId = await SeedCategoryAsync(ctx, masterName: "Audio", description: "Speakers and headphones");
        using (var seedScope = ctx.CreateScope())
        {
            var translations = seedScope.ServiceProvider.GetRequiredService<ITranslationRepository>();
            await translations.UpsertAsync(
            [
                new CatalogTranslationEntity
                {
                    EntityType = CatalogTranslationEntityType.Category,
                    EntityId = categoryId,
                    Language = "es-MX",
                    FieldName = "name",
                    TranslatedValue = "Audio (ES)",
                    IsMachineTranslated = true,
                },
                new CatalogTranslationEntity
                {
                    EntityType = CatalogTranslationEntityType.Category,
                    EntityId = categoryId,
                    Language = "es-MX",
                    FieldName = "description",
                    TranslatedValue = "Bocinas y audífonos",
                    IsMachineTranslated = true,
                },
            ]);
        }

        using var scope = ctx.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPolarCatalogReader>();
        var category = await reader.GetCategoryLocalizedAsync(new CategoryId(categoryId), "es-MX");

        Assert.NotNull(category);
        Assert.Equal("Audio (ES)", category.Name);
        Assert.Equal("Audio (ES)", category.MasterName);                 // both fields reflect the translation
        Assert.Equal("Bocinas y audífonos", category.Description);
    }

    [Fact]
    public async Task GetCategoryLocalizedAsync_with_missing_category_returns_null()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);

        using var scope = ctx.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPolarCatalogReader>();
        var category = await reader.GetCategoryLocalizedAsync(new CategoryId(Guid.NewGuid()), "es-MX");

        Assert.Null(category);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedProductAsync(CatalogTestContext ctx, string masterName, string? masterDescription = null)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.Products.Add(new LocalProductEntity
        {
            Id = id,
            MasterName = masterName,
            MasterDescription = masterDescription,
            MasterLanguage = "en-US",
            Kind = ProductKind.Product,
            HasVariants = false,
            PriceJson = """{"Kind":3,"Currency":"USD","IsRecurring":false}""",
            AttachedBenefitsJson = "[]",
            Status = PublishStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedCategoryAsync(CatalogTestContext ctx, string masterName, string? description = null)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.Categories.Add(new LocalCategoryEntity
        {
            Id = id,
            MasterName = masterName,
            Description = description,
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task SeedTranslationAsync(CatalogTestContext ctx, Guid productId, string language, string fieldName, string translatedValue)
    {
        using var scope = ctx.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITranslationRepository>();
        await repo.UpsertAsync(
        [
            new CatalogTranslationEntity
            {
                EntityType = CatalogTranslationEntityType.Product,
                EntityId = productId,
                Language = language,
                FieldName = fieldName,
                TranslatedValue = translatedValue,
                IsMachineTranslated = true,
            },
        ]);
    }
}
