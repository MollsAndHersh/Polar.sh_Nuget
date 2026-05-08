using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.Logging;
using System.Net.Http.Headers;

namespace PolarSharp.Auth;

/// <summary>
/// A <see cref="DelegatingHandler"/> that injects the <c>Authorization: Bearer</c> header
/// on every outbound Polar API request.
/// </summary>
/// <remarks>
/// Uses <see cref="IOptionsMonitor{TOptions}"/> so that a hot-reloaded access token takes effect
/// on the next outbound request without an application restart.
/// <para>
/// The raw token value is <strong>never</strong> written to any log sink. The handler scrubs
/// the <c>Authorization</c> header from all <see cref="ILogger"/> debug output via a redacting
/// log scope established before each request.
/// </para>
/// </remarks>
internal sealed class BearerTokenHandler(
    IOptionsMonitor<PolarOptions> options,
    ILogger<BearerTokenHandler> logger) : DelegatingHandler
{
    /// <summary>
    /// Injects the Bearer token and forwards the request to the next handler in the pipeline.
    /// </summary>
    /// <param name="request">The outgoing HTTP request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The HTTP response from the next handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = options.CurrentValue.AccessToken;

        var rawScope = new Dictionary<string, object?>
        {
            ["polar.auth"] = "Bearer ***",
        };
        var scope = PolarScopeBuilder.Build(rawScope, options.CurrentValue.Logging);
        using var _ = logger.BeginScope(scope);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, ct);
    }
}
