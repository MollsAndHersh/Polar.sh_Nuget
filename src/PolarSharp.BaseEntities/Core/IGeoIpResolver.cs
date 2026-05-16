using System.Net;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Optional geo-location resolver for IP addresses. Implementations look up an IP and
/// return city / region / country information; hosts wire their preferred service
/// (MaxMind GeoIP2, ipapi.co, IP2Location, etc.) via this abstraction.
/// </summary>
/// <remarks>
/// <para>
/// PolarSharp ships <c>NoOpGeoIpResolver</c> as the default — returns <see langword="null"/>
/// for every input. Geo enrichment is opt-in via host registration of a real implementation.
/// </para>
/// <para>
/// <strong>Privacy posture</strong>: no PII goes to PolarSharp's own infrastructure.
/// The host registers their preferred geo provider; PolarSharp never directly contacts
/// any geo service. The resolved <see cref="GeoLocation"/> is recorded on wallet events
/// + audit log entries when the tenant has IP capture enabled, and is subject to the
/// same retention policies as the IP itself.
/// </para>
/// </remarks>
public interface IGeoIpResolver
{
    /// <summary>Resolves geographic information for the given IP address.</summary>
    /// <param name="ip">The IP address to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="GeoLocation"/>, or <see langword="null"/> when unresolvable.</returns>
    Task<GeoLocation?> ResolveAsync(IPAddress ip, CancellationToken ct = default);
}

/// <summary>Geo-location information derived from an IP address.</summary>
/// <param name="City">City name (best-effort).</param>
/// <param name="Region">Region / state / province (best-effort).</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code.</param>
/// <param name="Latitude">Decimal degrees latitude (best-effort; may be <see langword="null"/>).</param>
/// <param name="Longitude">Decimal degrees longitude (best-effort; may be <see langword="null"/>).</param>
public sealed record GeoLocation(string City, string Region, string Country, double? Latitude, double? Longitude);

/// <summary>Default no-op geo-IP resolver. Always returns <see langword="null"/>.</summary>
/// <remarks>
/// Registered by default when no other <see cref="IGeoIpResolver"/> is wired by the host.
/// Hosts who want geo enrichment register MaxMind / ipapi.co / IP2Location / etc.
/// </remarks>
public sealed class NoOpGeoIpResolver : IGeoIpResolver
{
    /// <inheritdoc/>
    public Task<GeoLocation?> ResolveAsync(IPAddress ip, CancellationToken ct = default)
        => Task.FromResult<GeoLocation?>(null);
}
