using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.EcommerceStoreManagement.Cloning;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;

/// <summary>DI registration for the five EF-backed cloning services.</summary>
public static class CloningServicesExtensions
{
    /// <summary>
    /// Registers <see cref="IProductCloningService"/>, <see cref="ICategoryCloningService"/>,
    /// <see cref="IBenefitCloningService"/>, <see cref="IDiscountCloningService"/>, and
    /// <see cref="ICheckoutLinkCloningService"/>.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Each service is registered <see cref="ServiceLifetime.Scoped"/> so it shares the
    /// per-request <see cref="PolarCatalogDbContext"/> instance.
    /// </remarks>
    public static IServiceCollection AddPolarCatalogCloning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IProductCloningService, EfProductCloningService>();
        services.AddScoped<ICategoryCloningService, EfCategoryCloningService>();
        services.AddScoped<IBenefitCloningService, EfBenefitCloningService>();
        services.AddScoped<IDiscountCloningService, EfDiscountCloningService>();
        services.AddScoped<ICheckoutLinkCloningService, EfCheckoutLinkCloningService>();
        return services;
    }
}
