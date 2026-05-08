using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace PolarSharp.Health;

/// <summary>
/// An <see cref="IHealthCheck"/> that verifies connectivity to the Polar.sh API by calling
/// <c>GET /v1/organizations?limit=1</c> with the configured access token.
/// </summary>
/// <remarks>
/// Returns:
/// <list type="bullet">
///   <item><see cref="HealthStatus.Healthy"/> — Polar responded with HTTP 200.</item>
///   <item><see cref="HealthStatus.Degraded"/> — Polar responded with HTTP 429 (reachable but rate-limited).</item>
///   <item><see cref="HealthStatus.Unhealthy"/> — Any other failure (network error, 5xx, 401/403).</item>
/// </list>
/// <para>
/// Registered via <c>services.AddHealthChecks()</c> with tag <c>"polar"</c>.
/// The host app controls the endpoint mapping via <c>app.MapHealthChecks("/health")</c>.
/// </para>
/// </remarks>
internal sealed class PolarHealthCheck(
    IHttpClientFactory httpClientFactory,
    ILogger<PolarHealthCheck> logger) : IHealthCheck
{
    private const string ClientName = "PolarSharp";

    /// <summary>
    /// Checks Polar.sh API reachability.
    /// </summary>
    /// <param name="context">Health check context provided by the host.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="HealthCheckResult"/> describing the API connectivity state.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            using var response = await client
                .GetAsync("organizations?limit=1", HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return HealthCheckResult.Healthy("Polar API reachable.");

            if ((int)response.StatusCode == 429)
            {
                logger.LogWarning("PolarSharp health check: Polar returned 429 (rate-limited).");
                return HealthCheckResult.Degraded(
                    "Polar API reachable but rate-limited.",
                    data: new Dictionary<string, object> { ["statusCode"] = 429 });
            }

            logger.LogError(
                "PolarSharp health check: Polar returned unexpected status {StatusCode}.",
                (int)response.StatusCode);

            return HealthCheckResult.Unhealthy(
                $"Polar API returned unexpected status {(int)response.StatusCode}.",
                data: new Dictionary<string, object> { ["statusCode"] = (int)response.StatusCode });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "PolarSharp health check: failed to reach Polar API.");
            return HealthCheckResult.Unhealthy("Polar API unreachable.", ex);
        }
    }
}
