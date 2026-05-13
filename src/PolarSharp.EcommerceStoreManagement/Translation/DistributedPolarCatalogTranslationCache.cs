using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace PolarSharp.EcommerceStoreManagement.Translation;

/// <summary>
/// Distributed translation cache backed by ASP.NET Core's
/// <see cref="IDistributedCache"/> — works with Redis, SQL Server distributed cache, etc.
/// </summary>
/// <remarks>
/// Serializes <see cref="EntityTranslations"/> as JSON. The host must register
/// <see cref="IDistributedCache"/> via the standard ASP.NET Core APIs
/// (<c>AddStackExchangeRedisCache</c>, <c>AddDistributedSqlServerCache</c>, etc.).
/// </remarks>
public sealed class DistributedPolarCatalogTranslationCache : IPolarCatalogTranslationCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDistributedCache _cache;
    private readonly TranslationCacheOptions _options;

    /// <summary>Initializes the cache.</summary>
    public DistributedPolarCatalogTranslationCache(IDistributedCache cache, IOptions<TranslationCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        _cache = cache;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async ValueTask<EntityTranslations?> TryGetAllForEntityAsync(
        string tenantId, CatalogTranslationEntityType entityType, Guid entityId, CancellationToken ct = default)
    {
        var bytes = await _cache.GetAsync(MemoryPolarCatalogTranslationCache.BuildKey(tenantId, entityType, entityId), ct).ConfigureAwait(false);
        if (bytes is null) return null;
        return JsonSerializer.Deserialize<EntityTranslations>(bytes, JsonOptions);
    }

    /// <inheritdoc/>
    public async ValueTask SetAllForEntityAsync(
        string tenantId, CatalogTranslationEntityType entityType, Guid entityId,
        EntityTranslations translations, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(translations);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(translations, JsonOptions);
        await _cache.SetAsync(
            MemoryPolarCatalogTranslationCache.BuildKey(tenantId, entityType, entityId),
            bytes,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.AbsoluteExpirationMinutes) },
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask InvalidateAsync(
        string tenantId, CatalogTranslationEntityType entityType, Guid entityId, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(MemoryPolarCatalogTranslationCache.BuildKey(tenantId, entityType, entityId), ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask InvalidateManyAsync(
        string tenantId, IEnumerable<(CatalogTranslationEntityType EntityType, Guid EntityId)> entities, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        // No batch RemoveAsync on IDistributedCache; loop sequentially. Redis-backed providers
        // typically pipeline this internally.
        foreach (var (et, id) in entities)
        {
            await _cache.RemoveAsync(MemoryPolarCatalogTranslationCache.BuildKey(tenantId, et, id), ct).ConfigureAwait(false);
        }
    }
}
