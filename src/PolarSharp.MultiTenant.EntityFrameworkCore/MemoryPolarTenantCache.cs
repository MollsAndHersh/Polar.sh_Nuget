using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// In-process tenant cache backed by <see cref="IMemoryCache"/>.
/// </summary>
/// <remarks>
/// <para>
/// Default registration when no distributed cache is required. Suitable for single-instance
/// deployments or for multi-instance deployments where eventual consistency is acceptable
/// (each instance's cache populates independently from the underlying SQL store).
/// </para>
/// <para>
/// For multi-instance deployments needing immediate cross-instance invalidation, register
/// a distributed cache implementation that wraps <c>IDistributedCache</c> (Redis, SQL Server
/// distributed cache, etc.) instead.
/// </para>
/// </remarks>
public sealed class MemoryPolarTenantCache : IPolarTenantCache
{
    private readonly IMemoryCache _cache;
    private readonly PolarTenantCacheOptions _options;

    /// <summary>Initializes the cache with the supplied <see cref="IMemoryCache"/> and configured expirations.</summary>
    /// <param name="cache">The shared memory cache instance.</param>
    /// <param name="options">Cache expiration options.</param>
    public MemoryPolarTenantCache(IMemoryCache cache, IOptions<PolarTenantCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        _cache = cache;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public ValueTask<PolarTenantInfo?> TryGetByIdAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        _cache.TryGetValue(IdKey(id), out PolarTenantInfo? info);
        return ValueTask.FromResult(info);
    }

    /// <inheritdoc/>
    public ValueTask<PolarTenantInfo?> TryGetByIdentifierAsync(string identifier, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        _cache.TryGetValue(IdentifierKey(identifier), out PolarTenantInfo? info);
        return ValueTask.FromResult(info);
    }

    /// <inheritdoc/>
    public ValueTask SetAsync(PolarTenantInfo info, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (string.IsNullOrEmpty(info.Id)) return ValueTask.CompletedTask;

        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.AbsoluteExpirationMinutes),
            SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes),
        };

        _cache.Set(IdKey(info.Id), info, entryOptions);
        if (!string.IsNullOrEmpty(info.Identifier))
        {
            _cache.Set(IdentifierKey(info.Identifier), info, entryOptions);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask InvalidateAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        // Best-effort removal — ID-keyed entry first, then attempt to walk through to remove
        // the identifier-keyed entry as well via a join query. Since we don't have the
        // identifier directly, we rely on the eventual expiration of the identifier entry.
        // Future enhancement: maintain a reverse lookup table to enable atomic dual-removal.
        if (_cache.TryGetValue(IdKey(id), out PolarTenantInfo? info))
        {
            _cache.Remove(IdKey(id));
            if (info is not null && !string.IsNullOrEmpty(info.Identifier))
            {
                _cache.Remove(IdentifierKey(info.Identifier));
            }
        }
        return ValueTask.CompletedTask;
    }

    private static string IdKey(string id) => $"polar:tenant:id:{id}";
    private static string IdentifierKey(string identifier) => $"polar:tenant:identifier:{identifier}";
}
