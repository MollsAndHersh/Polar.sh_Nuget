namespace PolarSharp.EcommerceStorefronts.GuestSessions;

/// <summary>
/// Per-browser anonymous session identifying a guest customer. Carried in a signed
/// cookie; used to key the guest's cart and (optionally) recent-view history.
/// </summary>
/// <remarks>
/// Distinct from an authenticated customer identity — a single guest session may be
/// promoted to an authenticated customer when the visitor signs up at checkout, at
/// which point cart contents are transferred onto the new customer.
/// </remarks>
public sealed record GuestSession
{
    /// <summary>The session's stable identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>UTC timestamp the session was first created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC timestamp the session will expire if no further activity occurs.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Free-form metadata tagged onto the session — typically the original landing-page
    /// referrer, UTM parameters, and feature flags. Implementations should treat the
    /// dictionary as immutable.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
}
