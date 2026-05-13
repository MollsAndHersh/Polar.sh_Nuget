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
/// Smoke tests for <c>EfCategoryCloningService</c>. The distinctive guarantee for categories
/// is that collision-check is <b>parent-scoped</b> — the same MasterName can repeat under
/// different parents.
/// </summary>
public sealed class EfCategoryCloningServiceTests
{
    [Fact]
    public async Task Clone_auto_suffixes_within_same_parent_scope()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedCategoryAsync(ctx, name: "Audio");

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Audio (Copy)", result.ValueOrThrow().MasterName);
    }

    [Fact]
    public async Task Clone_collision_check_is_scoped_to_the_new_parent()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var electronics = await SeedCategoryAsync(ctx, name: "Electronics");
        var clothing = await SeedCategoryAsync(ctx, name: "Clothing");

        // Seed "Audio" under Electronics, then also seed "Audio (Copy)" under Electronics so
        // the same-parent collision check would skip to "Audio (Copy 2)".
        var audioUnderElectronics = await SeedCategoryAsync(ctx, name: "Audio", parentCategoryId: electronics.Value);
        await SeedCategoryAsync(ctx, name: "Audio (Copy)", parentCategoryId: electronics.Value);

        // Cloning Audio under Clothing should produce "Audio (Copy)" — because under Clothing
        // the auto-suffixed name does NOT collide (the existing Audio (Copy) lives under Electronics).
        var result = await CloneAsync(ctx, audioUnderElectronics, overrides: new CloneCategoryOverrides
        {
            NewParentCategoryId = clothing,
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Audio (Copy)", result.ValueOrThrow().MasterName);

        // Cloning Audio again under Electronics (its original parent) — now the collision check
        // sees both "Audio" AND "Audio (Copy)" under Electronics and lands at "Audio (Copy 2)".
        var sameParentResult = await CloneAsync(ctx, audioUnderElectronics);
        Assert.True(sameParentResult.IsSuccess);
        Assert.Equal("Audio (Copy 2)", sameParentResult.ValueOrThrow().MasterName);
    }

    [Fact]
    public async Task Clone_with_IncludeTranslations_duplicates_translation_rows()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedCategoryAsync(ctx, name: "Audio");

        using (var seedScope = ctx.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Translations.Add(new CatalogTranslationEntity
            {
                Id = Guid.NewGuid(),
                EntityType = CatalogTranslationEntityType.Category,
                EntityId = sourceId.Value,
                Language = "es-MX",
                FieldName = "name",
                TranslatedValue = "Audio (ES)",
                IsMachineTranslated = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        using var scope = ctx.CreateScope();
        var verifyDb = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var newId = Guid.Parse(result.ValueOrThrow().Id);
        var translations = await verifyDb.Translations
            .Where(t => t.EntityType == CatalogTranslationEntityType.Category && t.EntityId == newId)
            .ToListAsync();
        Assert.Single(translations);
        Assert.Equal("Audio (ES)", translations[0].TranslatedValue);
    }

    [Fact]
    public async Task Clone_cross_tenant_returns_SourceNotFound()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: "tenant-A",
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedCategoryAsync(ctx, name: "Audio");

        ctx.SetCurrentTenant("tenant-B");
        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsFailure);
        Assert.Equal(CloningErrorKind.SourceNotFound, result.ErrorOrThrow().Kind);
    }

    private static async Task<CategoryId> SeedCategoryAsync(CatalogTestContext ctx, string name, Guid? parentCategoryId = null)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.Categories.Add(new LocalCategoryEntity
        {
            Id = id,
            MasterName = name,
            ParentCategoryId = parentCategoryId,
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return new CategoryId(id);
    }

    private static async Task<Result<LocalCategory, CloningError>> CloneAsync(
        CatalogTestContext ctx,
        CategoryId source,
        CloneCategoryOverrides? overrides = null,
        CloneCategoryOptions? options = null)
    {
        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICategoryCloningService>();
        return await svc.CloneAsync(source, overrides, options);
    }
}
