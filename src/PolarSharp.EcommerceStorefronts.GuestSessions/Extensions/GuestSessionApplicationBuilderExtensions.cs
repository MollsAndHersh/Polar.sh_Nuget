using Microsoft.AspNetCore.Builder;

namespace PolarSharp.EcommerceStorefronts.GuestSessions.Extensions;

/// <summary>
/// Application-builder extensions for the guest-session middleware.
/// </summary>
public static class GuestSessionApplicationBuilderExtensions
{
    /// <summary>Adds <see cref="GuestSessionMiddleware"/> to the request pipeline.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Must be placed after <c>UseRouting()</c> and before any endpoint that reads
    /// the guest session from <c>HttpContext.Items</c>.
    /// </remarks>
    public static IApplicationBuilder UsePolarGuestSessions(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<GuestSessionMiddleware>();
    }
}
