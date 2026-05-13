using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace PolarSharp.EcommerceStoreManagement.Translation;

/// <summary>Single-process in-memory cache. Default when no distributed cache is registered.</summary>
public sealed class MemoryPolarCatalogTranslationCache : IPolarCatalogTranslationCache
{
    private readonly IMemoryCache _cache;
    private readonly TranslationCacheOptions _options;

    /// <summary>Initializes the cache.</summary>
    public MemoryPolarCatalogTranslationCache(IMemoryCache cache, IOptions<TranslationCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        _cache = cache;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public ValueTask<EntityTranslations?> TryGetAllForEntityAsync(
        string tenantId, CatalogTranslationEntityType entityType, Guid entityId, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, entityType, entityId);
        _cache.TryGetValue(key, out EntityTranslations? value);
        return ValueTask.FromResult(value);
    }

    /// <inheritdoc/>
    public ValueTask SetAllForEntityAsync(
        string tenantId, CatalogTranslationEntityType entityType, Guid entityId,
        EntityTranslations translations, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(translations);
        var key = BuildKey(tenantId, entityType, entityId);
        _cache.Set(key, translations, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.AbsoluteExpirationMinutes),
        });
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask InvalidateAsync(
        string tenantId, CatalogTranslationEntityType entityType, Guid entityId, CancellationToken ct = default)
    {
        _cache.Remove(BuildKey(tenantId, entityType, entityId));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask InvalidateManyAsync(
        string tenantId, IEnumerable<(CatalogTranslationEntityType EntityType, Guid EntityId)> entities, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        foreach (var (et, id) in entities) _cache.Remove(BuildKey(tenantId, et, id));
        return ValueTask.CompletedTask;
    }

    internal static string BuildKey(string tenantId, CatalogTranslationEntityType entityType, Guid entityId)
        => $"xlate:{tenantId}:{entityType}:{entityId:N}";
}

/// <summary>Configuration for the translation cache. Bound from <c>PolarSharp:EcommerceStoreManagement:TranslationCache</c>.</summary>
public sealed class TranslationCacheOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PolarSharp:EcommerceStoreManagement:TranslationCache";

    /// <summary>Master switch.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cache provider: <c>Memory</c>, <c>Distributed</c>, or <c>None</c>.</summary>
    public string Provider { get; set; } = "Memory";

    /// <summary>How long cached entries live before expiring. Default 4 hours.</summary>
    public int AbsoluteExpirationMinutes { get; set; } = 240;

    /// <summary>When true, every entity GET triggers a fire-and-forget pre-warm of that entity's translation set.</summary>
    public bool WarmOnReadEnabled { get; set; } = true;

    /// <summary>Per-warm budget. Warms that take longer are abandoned without affecting the originating request.</summary>
    public int WarmOnReadTimeoutSeconds { get; set; } = 5;
}
