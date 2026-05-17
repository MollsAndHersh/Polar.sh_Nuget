using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStorefronts.Extensions;
using PolarSharp.EcommerceStorefronts.GuestSessions.Extensions;

namespace PolarSharp.EcommerceStorefronts.AspNetCore.Extensions;

/// <summary>
/// Composition root for the storefront feature. Wires storefront-core services +
/// guest-session services in one call so hosts only have one line to add to
/// <c>Program.cs</c>.
/// </summary>
public static class PolarStorefrontsServiceCollectionExtensions
{
    /// <summary>Registers every storefront-feature service against the host's DI container.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configure">Optional callback for tuning <see cref="StorefrontOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// This is the ONE-LINE composition hosts call. Bridge packages
    /// (<c>PolarSharp.EcommerceStorefronts.Polar.Catalog</c>, <c>...Polar.Identity</c>,
    /// shipping providers, tax providers, etc.) layer their own <c>AddPolar*</c> calls
    /// on top to register concrete provider implementations.
    /// </remarks>
    public static IServiceCollection AddPolarStorefronts(
        this IServiceCollection services,
        Action<StorefrontOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddPolarStorefrontsCore(configure);
        services.AddPolarGuestSessions();
        return services;
    }
}
