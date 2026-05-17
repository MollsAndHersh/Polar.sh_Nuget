using Microsoft.AspNetCore.Builder;
using PolarSharp.EcommerceStorefronts.GuestSessions.Extensions;

namespace PolarSharp.EcommerceStorefronts.AspNetCore.Extensions;

/// <summary>
/// Pipeline-wiring extensions for the storefront feature.
/// </summary>
public static class PolarStorefrontsApplicationBuilderExtensions
{
    /// <summary>Wires every storefront-feature middleware into the request pipeline.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Phase 25 wires only the guest-session middleware. The SignalR hub that streams
    /// cart + inventory updates joins this composition in Phase 28; the contract of
    /// <see cref="UsePolarStorefronts"/> does not change.
    /// </remarks>
    public static IApplicationBuilder UsePolarStorefronts(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UsePolarGuestSessions();
        return app;
    }
}
