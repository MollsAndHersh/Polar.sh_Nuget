using Microsoft.Extensions.Options;

namespace PolarSharp.Versioning;

/// <summary>
/// A <see cref="DelegatingHandler"/> that injects the <c>Polar-Version</c> date header on
/// every outbound request, pinning the SDK to a known Polar API schema version.
/// </summary>
/// <remarks>
/// Uses <see cref="IOptionsMonitor{TOptions}"/> so that a hot-reloaded <c>ApiVersion</c>
/// setting takes effect on the next outbound call without an application restart.
/// <para>
/// Pipeline position: after <see cref="Auth.BearerTokenHandler"/> and <see cref="Idempotency.IdempotencyKeyHandler"/>,
/// before the resilience handler. This ensures the version header is present on every retry attempt.
/// </para>
/// </remarks>
internal sealed class ApiVersionHandler(IOptionsMonitor<PolarOptions> options) : DelegatingHandler
{
    private const string PolarVersionHeader = "Polar-Version";

    /// <summary>
    /// Adds the <c>Polar-Version</c> header to every outbound request before forwarding to the next handler.
    /// </summary>
    /// <param name="request">The outgoing HTTP request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The HTTP response from the next handler in the pipeline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var version = options.CurrentValue.ApiVersion
                      ?? PolarApiMetadata.GeneratedAgainstVersion;

        request.Headers.Remove(PolarVersionHeader);
        request.Headers.Add(PolarVersionHeader, version);

        return base.SendAsync(request, ct);
    }
}
