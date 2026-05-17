namespace PolarSharp.EcommerceStorefronts;

/// <summary>
/// Tunables for the storefront feature, bound by
/// <c>AddPolarStorefronts(Action&lt;StorefrontOptions&gt;)</c>.
/// </summary>
/// <remarks>
/// Defaults are sensible for a small-to-mid SaaS storefront. Production deployments
/// typically only override <see cref="GuestSessionLifetime"/> and the cart-limit
/// values when load testing surfaces a need.
/// </remarks>
public sealed class StorefrontOptions
{
    /// <summary>Name of the cookie that carries the guest session identifier.</summary>
    public string GuestSessionCookieName { get; set; } = "polar_guest_session";

    /// <summary>How long a guest session is honoured before re-creation.</summary>
    public TimeSpan GuestSessionLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Hard cap on the number of distinct lines in a single cart.</summary>
    public int MaxCartLineItems { get; set; } = 100;

    /// <summary>
    /// Hard cap on the grand total of a single cart in minor units; default is
    /// ten thousand US dollars (<c>1_000_000</c>).
    /// </summary>
    public int MaxCartTotalValueCents { get; set; } = 1_000_000;

    /// <summary>HTTP header carrying the cart idempotency token.</summary>
    public string CartIdempotencyTokenHeader { get; set; } = "X-Storefront-Idempotency";
}
