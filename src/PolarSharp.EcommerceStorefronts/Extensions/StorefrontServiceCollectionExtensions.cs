using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Customers;
using PolarSharp.EcommerceStorefronts.Cart;
using PolarSharp.EcommerceStorefronts.Checkout;
using PolarSharp.EcommerceStorefronts.Customers;
using PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing;

namespace PolarSharp.EcommerceStorefronts.Extensions;

/// <summary>
/// Registration extensions for the storefront-core services.
/// </summary>
/// <remarks>
/// <see cref="AddPolarStorefrontsCore"/> registers the cart, checkout, and customer
/// service skeletons together with <see cref="IStorefrontClient"/> as scoped services.
/// Catalog, search, shipping, tax, and wallet providers are intentionally NOT
/// registered here — those are wired by bridge packages
/// (<c>PolarSharp.EcommerceStorefronts.Polar.Catalog</c> and friends).
/// <para>
/// Host applications normally call <c>AddPolarStorefronts()</c> on the AspNetCore
/// composition package; that wraps this core registration plus the guest-sessions
/// middleware in one call.
/// </para>
/// </remarks>
public static class StorefrontServiceCollectionExtensions
{
    /// <summary>Registers the storefront-core services + options.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configure">Optional callback for tuning <see cref="StorefrontOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPolarStorefrontsCore(
        this IServiceCollection services,
        Action<StorefrontOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<StorefrontOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddScoped<IStorefrontCartService, DefaultStorefrontCartService>();
        // The checkout service depends on an OPTIONAL OrderProcessingPipeline; use a
        // factory so the constructor's default-null parameter is honoured when the
        // pipeline package has not been registered.
        services.TryAddScoped<IStorefrontCheckoutService>(sp =>
            new DefaultStorefrontCheckoutService(sp.GetService<OrderProcessingPipeline>()));
        services.TryAddScoped<IStorefrontCustomerService, DefaultStorefrontCustomerService>();
        services.TryAddScoped<IStorefrontClient, StorefrontClient>();

        return services;
    }
}
