using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStorefronts;

namespace PolarSharp.EcommerceStorefronts.GuestSessions;

/// <summary>
/// Default <see cref="IGuestSessionService"/> backed by ASP.NET Core data-protection
/// signing keys. Reads + writes a cookie whose payload is the session JSON sealed by
/// <see cref="IDataProtectionProvider"/>.
/// </summary>
/// <remarks>
/// Phase 25 ships the scaffold + cookie configuration plumbing; the actual sign /
/// verify / serialise round-trip is filled in by Phase 25.x. The
/// <see cref="NotImplementedException"/> on the read/write members surfaces today if
/// an integration test happens to exercise the path, but normal Phase 25 work runs
/// without invoking guest-session resolution.
/// </remarks>
public sealed class SignedCookieGuestSessionService : IGuestSessionService
{
    private const string Purpose = "PolarSharp.EcommerceStorefronts.GuestSessions.v1";
    private const string NotImplementedMessage =
        "Signed-cookie guest session sign/verify is scheduled for Phase 25.x — see the storefront-core architecture section of the plan.";

    private readonly IDataProtector _protector;
    private readonly StorefrontOptions _options;

    /// <summary>Constructs the service over the supplied protector + options.</summary>
    /// <param name="protectionProvider">Source of signing keys.</param>
    /// <param name="options">Storefront tunables — cookie name, lifetime.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="protectionProvider"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public SignedCookieGuestSessionService(
        IDataProtectionProvider protectionProvider,
        IOptions<StorefrontOptions> options)
    {
        ArgumentNullException.ThrowIfNull(protectionProvider);
        ArgumentNullException.ThrowIfNull(options);
        _protector = protectionProvider.CreateProtector(Purpose);
        _options = options.Value;
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public GuestSession? TryRead(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = _protector;
        _ = _options;
        throw new NotImplementedException(NotImplementedMessage);
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public GuestSession Create(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        throw new NotImplementedException(NotImplementedMessage);
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public GuestSession Renew(HttpContext context, GuestSession session)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);
        throw new NotImplementedException(NotImplementedMessage);
    }

    /// <inheritdoc/>
    public void Destroy(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Destroying the cookie is safe to wire today — it is a simple delete on the
        // response and there is no cryptographic round-trip involved.
        context.Response.Cookies.Delete(_options.GuestSessionCookieName);
    }
}
