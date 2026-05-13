using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Verifies cache hit/miss semantics, invalidation on writes (single + bulk), and that the
/// per-entity cache key holds every language × every field for that entity.
/// </summary>
public sealed class TranslationCacheTests
{
    private static MemoryPolarCatalogTranslationCache NewCache() =>
        new(new MemoryCache(new MemoryCacheOptions()), Options.Create(new TranslationCacheOptions()));

    [Fact]
    public async Task Get_returns_null_on_miss()
    {
        var cache = NewCache();
        var result = await cache.TryGetAllForEntityAsync("t", CatalogTranslationEntityType.Product, Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task Set_then_get_returns_stored_translations()
    {
        var cache = NewCache();
        var entityId = Guid.NewGuid();
        var translations = new EntityTranslations(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["es-MX"] = new Dictionary<string, string> { ["name"] = "Hola Mundo" },
            ["fr-FR"] = new Dictionary<string, string> { ["name"] = "Bonjour le monde" },
        });

        await cache.SetAllForEntityAsync("t", CatalogTranslationEntityType.Product, entityId, translations);
        var result = await cache.TryGetAllForEntityAsync("t", CatalogTranslationEntityType.Product, entityId);

        Assert.NotNull(result);
        Assert.Equal("Hola Mundo", result.Get("es-MX", "name"));
        Assert.Equal("Bonjour le monde", result.Get("fr-FR", "name"));
    }

    [Fact]
    public async Task Get_with_missing_language_returns_null_per_field()
    {
        var cache = NewCache();
        var entityId = Guid.NewGuid();
        var translations = new EntityTranslations(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["es-MX"] = new Dictionary<string, string> { ["name"] = "Hola" },
        });
        await cache.SetAllForEntityAsync("t", CatalogTranslationEntityType.Product, entityId, translations);

        var result = await cache.TryGetAllForEntityAsync("t", CatalogTranslationEntityType.Product, entityId);
        Assert.Null(result!.Get("de-DE", "name"));      // language missing
        Assert.Null(result.Get("es-MX", "description")); // field missing
    }

    [Fact]
    public async Task Invalidate_removes_the_cache_entry()
    {
        var cache = NewCache();
        var entityId = Guid.NewGuid();
        await cache.SetAllForEntityAsync("t", CatalogTranslationEntityType.Product, entityId, EntityTranslations.Empty);

        await cache.InvalidateAsync("t", CatalogTranslationEntityType.Product, entityId);

        var after = await cache.TryGetAllForEntityAsync("t", CatalogTranslationEntityType.Product, entityId);
        Assert.Null(after);
    }

    [Fact]
    public async Task InvalidateMany_removes_every_supplied_entity()
    {
        var cache = NewCache();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids)
            await cache.SetAllForEntityAsync("t", CatalogTranslationEntityType.Product, id, EntityTranslations.Empty);

        await cache.InvalidateManyAsync(
            "t",
            ids.Select(id => (CatalogTranslationEntityType.Product, id)));

        foreach (var id in ids)
        {
            var after = await cache.TryGetAllForEntityAsync("t", CatalogTranslationEntityType.Product, id);
            Assert.Null(after);
        }
    }

    [Fact]
    public async Task Tenant_scope_is_part_of_the_cache_key()
    {
        var cache = NewCache();
        var entityId = Guid.NewGuid();
        var translations = new EntityTranslations(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["es-MX"] = new Dictionary<string, string> { ["name"] = "Tenant A's value" },
        });
        await cache.SetAllForEntityAsync("tenant-a", CatalogTranslationEntityType.Product, entityId, translations);

        var sameIdDifferentTenant = await cache.TryGetAllForEntityAsync("tenant-b", CatalogTranslationEntityType.Product, entityId);
        Assert.Null(sameIdDifferentTenant);   // tenant-b cannot read tenant-a's cache entry
    }
}
