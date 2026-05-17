using Microsoft.AspNetCore.Http;

namespace PolarSharp.EcommerceStorefronts.GuestSessions;

/// <summary>
/// Reads + writes guest session cookies on the current HTTP context.
/// </summary>
/// <remarks>
/// The service is the only place that knows about the cookie format. Cart, checkout,
/// and customer code receives a fully-validated <see cref="GuestSession"/> via
/// <see cref="HttpContext.Items"/> wired by <see cref="GuestSessionMiddleware"/>.
/// </remarks>
public interface IGuestSessionService
{
    /// <summary>
    /// Reads the guest session from the cookie on <paramref name="context"/>, returning
    /// <see langword="null"/> if the cookie is absent, expired, or fails signature
    /// verification.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The parsed session, or <see langword="null"/>.</returns>
    GuestSession? TryRead(HttpContext context);

    /// <summary>Creates a fresh session and writes the cookie back to the response.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The newly created session.</returns>
    GuestSession Create(HttpContext context);

    /// <summary>
    /// Extends <paramref name="session"/>'s expiry by the configured lifetime and rewrites
    /// the cookie.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="session">The session to renew.</param>
    /// <returns>The session with its <c>ExpiresAt</c> bumped.</returns>
    GuestSession Renew(HttpContext context, GuestSession session);

    /// <summary>Removes the guest session cookie (used at customer sign-up / sign-in time).</summary>
    /// <param name="context">The current HTTP context.</param>
    void Destroy(HttpContext context);
}
