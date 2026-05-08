using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace PolarSharp.Connection;

/// <summary>
/// Creates and configures <see cref="SocketsHttpHandler"/> instances with PolarSharp-optimized
/// settings for connection pooling, DNS refresh, TLS hardening, and HTTP/2 multiplexing.
/// </summary>
/// <remarks>
/// Replaces the legacy <see cref="HttpClientHandler"/> with the modern .NET transport layer.
/// Defaults are tuned for high-throughput cloud API workloads:
/// <list type="bullet">
///   <item>DNS refresh every 15 minutes (prevents stale cloud LB IP binding).</item>
///   <item>HTTP/2 enabled with multiple simultaneous connections per host.</item>
///   <item>Auto-redirect disabled (SSRF defense).</item>
///   <item>TLS 1.2/1.3 only with CRL checking.</item>
/// </list>
/// </remarks>
internal sealed class PolarSocketsHandlerFactory(IOptionsMonitor<PolarOptions> options)
{
    /// <summary>
    /// Creates a new <see cref="SocketsHttpHandler"/> configured from <see cref="PolarConnectionOptions"/>.
    /// </summary>
    /// <returns>A configured <see cref="SocketsHttpHandler"/> ready to use as a primary HTTP handler.</returns>
    public SocketsHttpHandler Create()
    {
        var conn = options.CurrentValue.Connection;

        return new SocketsHttpHandler
        {
            // Force DNS re-resolution on a schedule — cloud LBs rotate IPs every 5–15 min.
            PooledConnectionLifetime = TimeSpan.FromMinutes(conn.PooledConnectionLifetimeMinutes),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(conn.PooledConnectionIdleTimeoutMinutes),
            MaxConnectionsPerServer = conn.MaxConnectionsPerServer,

            // HTTP/2 multiplexing: one TCP connection handles hundreds of concurrent streams.
            EnableMultipleHttp2Connections = conn.EnableMultipleHttp2Connections,

            // SSRF defense: PolarSharp never follows redirects silently.
            AllowAutoRedirect = false,

            // TLS hardening
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.Online,
            },

            // Automatic decompression for Polar's gzip responses
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli,
        };
    }
}
