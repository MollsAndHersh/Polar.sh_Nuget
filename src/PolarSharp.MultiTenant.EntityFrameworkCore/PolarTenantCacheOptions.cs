namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Configuration for the tenant cache layer. Bound from
/// <c>PolarSharp:MultiTenant:TenantCache</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class PolarTenantCacheOptions
{
    /// <summary>Gets or sets a value indicating whether the cache is enabled.</summary>
    /// <value>Default <see langword="true"/>. Set to <see langword="false"/> to bypass caching entirely (every lookup hits the database).</value>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the cache backing provider.</summary>
    /// <value>
    /// <see cref="PolarTenantCacheProvider.Memory"/> (default, single-process), or
    /// <see cref="PolarTenantCacheProvider.Distributed"/> (wraps the host's <c>IDistributedCache</c>).
    /// </value>
    public PolarTenantCacheProvider Provider { get; set; } = PolarTenantCacheProvider.Memory;

    /// <summary>Gets or sets the absolute expiration in minutes.</summary>
    /// <value>Default 60. Range 1–1440.</value>
    public int AbsoluteExpirationMinutes { get; set; } = 60;

    /// <summary>Gets or sets the sliding expiration in minutes.</summary>
    /// <value>Default 15. Range 1–1440. Must be ≤ <see cref="AbsoluteExpirationMinutes"/>.</value>
    public int SlidingExpirationMinutes { get; set; } = 15;
}

/// <summary>Tenant cache backing provider selection.</summary>
public enum PolarTenantCacheProvider
{
    /// <summary>In-process <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>. Default. Suitable for single-instance deployments.</summary>
    Memory,

    /// <summary>Wraps the host-registered <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>. Suitable for multi-instance deployments needing immediate cross-instance invalidation.</summary>
    Distributed,

    /// <summary>Disable caching — every tenant lookup hits the database directly.</summary>
    None,
}
