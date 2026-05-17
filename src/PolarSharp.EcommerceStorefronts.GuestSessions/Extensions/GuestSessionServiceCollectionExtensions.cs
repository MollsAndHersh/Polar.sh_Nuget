using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PolarSharp.EcommerceStorefronts.GuestSessions.Extensions;

/// <summary>
/// Registration extensions for guest-session services.
/// </summary>
/// <remarks>
/// Hosts normally do not call this directly — it is wired by
/// <c>AddPolarStorefronts()</c> on the AspNetCore composition package.
/// </remarks>
public static class GuestSessionServiceCollectionExtensions
{
    /// <summary>Registers the default <see cref="IGuestSessionService"/> + data-protection wiring.</summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPolarGuestSessions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ASP.NET Core's AddDataProtection is idempotent — registering it here means hosts
        // that have not configured data protection explicitly still get a working
        // signed-cookie pipeline.
        services.AddDataProtection();

        services.TryAddScoped<IGuestSessionService, SignedCookieGuestSessionService>();

        return services;
    }
}
