using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;

/// <summary>
/// Top-level v1.3.G orchestrator that composes every EF-backed
/// <c>PolarSharp.EcommerceStoreManagement</c> service registration into a single
/// one-line call.
/// </summary>
/// <remarks>
/// <para>
/// Before this orchestrator existed, hosts had to call <c>AddPolarCatalogTranslation</c>,
/// <c>AddPolarCatalogServices</c>, and <c>AddPolarCatalogCloning</c> separately and
/// remember the right order. The single <see cref="AddPolarEcommerce"/> call composes
/// them; the underlying narrower extensions stay public for hosts that want
/// finer-grained control.
/// </para>
/// <para>
/// Hosts must still register the catalog DbContext separately via the provider package's
/// <c>UseSqliteCatalog</c> / <c>UseSqlServerCatalog</c> / <c>UsePostgreSqlCatalog</c>
/// extension before or after this call.
/// </para>
/// </remarks>
public static class AddPolarEcommerceExtensions
{
    /// <summary>
    /// Registers the full <c>PolarSharp.EcommerceStoreManagement</c> service surface:
    /// translation resolver + repository + reader + cache, refund service, license-key
    /// validator, business-profile service, inventory updater, the catalog publisher, and
    /// the five cloning services. Binds every options class from the supplied configuration.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">Application configuration containing the
    /// <c>PolarSharp:EcommerceStoreManagement:*</c> options sections.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPolarEcommerce(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddPolarCatalogTranslation(configuration);
        services.AddPolarCatalogServices(configuration);
        services.AddPolarCatalogCloning();
        return services;
    }
}
