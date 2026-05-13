namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Caching layer for tenant lookups — avoids a database round-trip on every API call /
/// webhook delivery / report query. Backed in-process by <c>MemoryPolarTenantCache</c> by
/// default, or wraps the host's registered <c>IDistributedCache</c> for multi-instance hosts.
/// </summary>
/// <remarks>
/// <para>
/// Tenants are cached by both <see cref="PolarTenantInfo.Id"/> and
/// <see cref="PolarTenantInfo.Identifier"/> — both lookup paths consult the cache first.
/// </para>
/// <para>
/// Cache invalidation occurs on every write through <c>EfMultiTenantStore</c>: an updated
/// tenant has its entry removed BEFORE the next read repopulates with the latest values.
/// </para>
/// </remarks>
public interface IPolarTenantCache
{
    /// <summary>Attempts to retrieve a cached tenant by its <see cref="PolarTenantInfo.Id"/>.</summary>
    /// <param name="id">The tenant's primary identifier (GUID string).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached tenant, or <see langword="null"/> on miss.</returns>
    ValueTask<PolarTenantInfo?> TryGetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>Attempts to retrieve a cached tenant by its <see cref="PolarTenantInfo.Identifier"/>.</summary>
    /// <param name="identifier">The tenant's human-readable slug.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached tenant, or <see langword="null"/> on miss.</returns>
    ValueTask<PolarTenantInfo?> TryGetByIdentifierAsync(string identifier, CancellationToken ct = default);

    /// <summary>Stores a tenant in the cache under both ID and Identifier keys.</summary>
    /// <param name="info">The tenant to cache.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask SetAsync(PolarTenantInfo info, CancellationToken ct = default);

    /// <summary>Removes a tenant from the cache (both ID and Identifier keys).</summary>
    /// <param name="id">The tenant's primary identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask InvalidateAsync(string id, CancellationToken ct = default);
}
