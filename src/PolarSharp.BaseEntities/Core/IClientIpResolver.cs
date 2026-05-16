using System.Net;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Resolves the true client IP address from a request, accounting for trusted proxies,
/// load balancers, and CDN edges that interpose between the client and the host.
/// </summary>
/// <remarks>
/// <para>
/// Hosts behind Cloudflare, AWS CloudFront, Azure Front Door, or any reverse proxy
/// receive the proxy's IP in <c>HttpContext.Connection.RemoteIpAddress</c> by default,
/// not the customer's. The customer's true IP lives in the <c>X-Forwarded-For</c> or
/// RFC 7239 <c>Forwarded</c> header(s).
/// </para>
/// <para>
/// PolarSharp ships two implementations:
/// </para>
/// <list type="bullet">
///   <item>
///     <c>DefaultClientIpResolver</c> — reads <c>HttpContext.Connection.RemoteIpAddress</c>.
///     Correct for hosts NOT behind a proxy. Default registration when nothing else is wired.
///   </item>
///   <item>
///     <c>ForwardedHeadersClientIpResolver</c> — reads <c>X-Forwarded-For</c> / <c>Forwarded</c>,
///     walks the header value backwards through the trusted-proxy list, returns the first
///     untrusted IP. Hosts behind any proxy MUST register this resolver + configure the trusted-proxy
///     list, OR fraud detection will treat every customer's IP as the proxy's IP (useless).
///   </item>
/// </list>
/// <para>
/// <strong>Security note</strong>: trusting forwarded headers without proper trusted-proxy
/// configuration is a security risk — any client can send a fake <c>X-Forwarded-For</c>.
/// The host MUST configure the resolver only when actually behind a proxy whose forwarded
/// headers are trustworthy.
/// </para>
/// </remarks>
public interface IClientIpResolver
{
    /// <summary>
    /// Resolves the true client IP from the request's HTTP context (or equivalent transport context).
    /// </summary>
    /// <param name="httpContextOrEquivalent">
    /// The host's request context. Typically <c>HttpContext</c> in ASP.NET Core; may be any
    /// transport-specific context type cast to <c>object</c> for cross-platform use.
    /// </param>
    /// <returns>The resolved IP address, or <see langword="null"/> when no IP can be determined.</returns>
    IPAddress? Resolve(object httpContextOrEquivalent);
}
