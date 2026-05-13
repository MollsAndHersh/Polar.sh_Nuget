using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Translation;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Tests for the v1.3.B <see cref="EfTranslationRepository"/>. Covers idempotent upserts
/// (insert when new, update when existing), per-entity reads, per-entity delete, and the
/// fake-data bulk delete used by the data-seeding cleanup path.
/// </summary>
public sealed class EfTranslationRepositoryTests
{
    private static void Configure(IServiceCollection s) =>
        s.AddScoped<ITranslationRepository, EfTranslationRepository>();

    [Fact]
    public async Task UpsertAsync_inserts_new_rows()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var productId = Guid.NewGuid();

        using var scope = ctx.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITranslationRepository>();
        await repo.UpsertAsync(
        [
            NewRow(productId, "es-MX", "name", "Camiseta Premium"),
            NewRow(productId, "es-MX", "description", "La mejor camiseta"),
            NewRow(productId, "fr-FR", "name", "T-shirt Premium"),
        ]);

        var rows = await repo.GetAllForEntityAsync(CatalogTranslationEntityType.Product, productId);
        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, r => r.Language == "es-MX" && r.FieldName == "name" && r.TranslatedValue == "Camiseta Premium");
    }

    [Fact]
    public async Task UpsertAsync_updates_existing_rows_in_place()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var productId = Guid.NewGuid();

        using var scope = ctx.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITranslationRepository>();
        await repo.UpsertAsync([NewRow(productId, "es-MX", "name", "Old translation")]);
        await repo.UpsertAsync([NewRow(productId, "es-MX", "name", "Better translation")]);

        var rows = await repo.GetAllForEntityAsync(CatalogTranslationEntityType.Product, productId);
        Assert.Single(rows);
        Assert.Equal("Better translation", rows[0].TranslatedValue);
        Assert.NotNull(rows[0].UpdatedAt);                       // UpdatedAt is set on update
    }

    [Fact]
    public async Task UpsertAsync_mixed_batch_inserts_and_updates_correctly()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var productId = Guid.NewGuid();

        using var scope = ctx.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITranslationRepository>();
        // Seed one existing row.
        await repo.UpsertAsync([NewRow(productId, "es-MX", "name", "Old name")]);

        // Mixed: existing + brand-new.
        await repo.UpsertAsync(
        [
            NewRow(productId, "es-MX", "name", "Updated name"),
            NewRow(productId, "es-MX", "description", "New description"),
            NewRow(productId, "fr-FR", "name", "Nouveau nom"),
        ]);

        var rows = await repo.GetAllForEntityAsync(CatalogTranslationEntityType.Product, productId);
        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, r => r.Language == "es-MX" && r.FieldName == "name" && r.TranslatedValue == "Updated name");
        Assert.Contains(rows, r => r.Language == "fr-FR" && r.FieldName == "name" && r.TranslatedValue == "Nouveau nom");
    }

    [Fact]
    public async Task GetAllForEntityAsync_returns_only_the_requested_entity_translations()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        using var scope = ctx.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITranslationRepository>();
        await repo.UpsertAsync(
        [
            NewRow(productA, "es-MX", "name", "Producto A"),
            NewRow(productB, "es-MX", "name", "Producto B"),
        ]);

        var aRows = await repo.GetAllForEntityAsync(CatalogTranslationEntityType.Product, productA);
        Assert.Single(aRows);
        Assert.Equal("Producto A", aRows[0].TranslatedValue);
    }

    [Fact]
    public async Task DeleteAllForEntityAsync_removes_only_that_entity_translations()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        using var scope = ctx.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITranslationRepository>();
        await repo.UpsertAsync(
        [
            NewRow(productA, "es-MX", "name", "A1"),
            NewRow(productA, "fr-FR", "name", "A2"),
            NewRow(productB, "es-MX", "name", "B1"),
        ]);

        await repo.DeleteAllForEntityAsync(CatalogTranslationEntityType.Product, productA);

        Assert.Empty(await repo.GetAllForEntityAsync(CatalogTranslationEntityType.Product, productA));
        Assert.Single(await repo.GetAllForEntityAsync(CatalogTranslationEntityType.Product, productB));
    }

    [Fact]
    public async Task DeleteAllFakeDataAsync_removes_only_fake_data_rows()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: Configure);
        var productId = Guid.NewGuid();

        using var scope = ctx.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITranslationRepository>();
        await repo.UpsertAsync(
        [
            NewRow(productId, "es-MX", "name", "Real product"),
            NewRow(productId, "fr-FR", "name", "Fake product", isFakeData: true),
        ]);

        await repo.DeleteAllFakeDataAsync();

        var rows = await repo.GetAllForEntityAsync(CatalogTranslationEntityType.Product, productId);
        Assert.Single(rows);
        Assert.False(rows[0].IsFakeData);
    }

    private static CatalogTranslationEntity NewRow(Guid entityId, string language, string fieldName, string value, bool isFakeData = false) =>
        new()
        {
            EntityType = CatalogTranslationEntityType.Product,
            EntityId = entityId,
            Language = language,
            FieldName = fieldName,
            TranslatedValue = value,
            IsMachineTranslated = true,
            IsFakeData = isFakeData,
        };
}
