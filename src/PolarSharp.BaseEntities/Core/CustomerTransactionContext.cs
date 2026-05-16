using System.Net;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Optional per-transaction context describing the customer's request origin.
/// Populated by the host application from <c>HttpContext</c> (or equivalent) and threaded
/// through every customer-bound PolarSharp service call. Storage shape is governed by
/// the tenant's <see cref="IpCaptureMode"/>.
/// </summary>
/// <remarks>
/// <para>
/// This record is the universal "request fingerprint" passed alongside customer-affecting
/// operations: refunds, license validation, wallet funding, wallet debits, checkout
/// completion, onboarding callbacks, manual credit issuance, etc.
/// </para>
/// <para>
/// Hosts that don't capture IP information (or don't want to for privacy) pass
/// <see cref="Empty"/> or omit the parameter entirely from PolarSharp service calls —
/// the services accept <see langword="null"/> and no information is recorded.
/// </para>
/// <para>
/// The host is responsible for obtaining the TRUE client IP — when behind a proxy, CDN,
/// or load balancer, this means reading the <c>X-Forwarded-For</c> or RFC 7239 <c>Forwarded</c>
/// header rather than <c>HttpContext.Connection.RemoteIpAddress</c>. PolarSharp ships
/// <c>IClientIpResolver</c> with a <c>ForwardedHeadersClientIpResolver</c> implementation
/// that handles this with a configurable trusted-proxy list (opt-in for security).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In a host controller:
/// var ctx = new CustomerTransactionContext
/// {
///     CustomerIp = clientIpResolver.Resolve(HttpContext),
///     UserAgent = Request.Headers.UserAgent.ToString(),
///     Referer = Request.Headers.Referer.ToString(),
/// };
/// await _refundService.IssueFullRefundAsync(orderId, reason, comment, ctx);
/// </code>
/// </example>
public sealed record CustomerTransactionContext
{
    /// <summary>The customer's IP address as observed by the host. May be null when not captured.</summary>
    public IPAddress? CustomerIp { get; init; }

    /// <summary>The HTTP User-Agent header from the customer's request. May be null when not captured.</summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Host-defined session-level fingerprint (e.g. a browser-fingerprint hash from the
    /// host's analytics layer). Optional; pass-through to audit log for fraud-analysis
    /// correlation across requests within the same browser session.
    /// </summary>
    public string? SessionFingerprint { get; init; }

    /// <summary>The HTTP Referer header from the customer's request. Useful for traffic-source attribution.</summary>
    public string? Referer { get; init; }

    /// <summary>The HTTP Accept-Language header from the customer's request. Used for locale resolution + fraud heuristics.</summary>
    public string? AcceptLanguage { get; init; }

    /// <summary>
    /// Empty context for code paths where no customer is involved (e.g. cron-triggered
    /// catalog publish, background subscription billing, etc.). All fields default null.
    /// </summary>
    public static CustomerTransactionContext Empty { get; } = new();
}
