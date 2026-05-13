using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Extensions;

/// <summary>
/// Provider-agnostic registration helpers for the EF Core-backed tenant store.
/// </summary>
/// <remarks>
/// Provider-specific extension methods (<c>.UseSqlServer()</c>, <c>.UseSqlite()</c>,
/// <c>.UsePostgreSql()</c>) live in their respective companion packages. Each calls
/// <see cref="AddCoreServices"/> to register the cross-provider services (cache, store impl,
/// scope initializer) before configuring the DbContext options.
/// </remarks>
public static class EfTenantStoreBuilderExtensions
{
    /// <summary>
    /// Registers the cross-provider services required by every EF-backed tenant store.
    /// Called by the provider packages' <c>.UseXxx()</c> extension methods.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">The application configuration (for binding cache options).</param>
    public static void AddCoreServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind cache options from PolarSharp:MultiTenant:TenantCache
        services.AddOptions<PolarTenantCacheOptions>()
            .Bind(configuration.GetSection("PolarSharp:MultiTenant:TenantCache"))
            .ValidateOnStart();

        // Memory cache is always registered (used by the default cache impl)
        services.AddMemoryCache();

        // Default cache provider is Memory; consumers can replace by registering a different
        // IPolarTenantCache implementation before AddPolarMultiTenant() is called.
        services.TryAddSingleton<IPolarTenantCache, MemoryPolarTenantCache>();
    }
}
