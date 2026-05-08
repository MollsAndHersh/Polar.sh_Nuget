using Microsoft.Extensions.Options;
using PolarSharp;

namespace PolarTestApp.Endpoints;

internal static class DiagnosticsEndpoints
{
    internal static WebApplication MapDiagnosticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/diagnostics").WithTags("Diagnostics");

        // GET /test/diagnostics/config
        // Returns the current PolarSharp configuration summary (non-sensitive values only).
        // The access token is always masked.
        group.MapGet("/config", (IOptions<PolarOptions> options) =>
        {
            var opts = options.Value;
            var token = opts.AccessToken;
            var maskedToken = MaskToken(token);

            return Results.Ok(new
            {
                mode = opts.Mode.ToString(),
                server = opts.Server.ToString(),
                basePath = opts.BasePath,
                apiVersion = opts.ApiVersion ?? $"{PolarClient.GeneratedAgainstVersion} (SDK default)",
                apiVersionStrictness = opts.ApiVersionStrictness.ToString(),
                accessToken = maskedToken,
                timeoutMs = opts.TimeoutMs,
                maxRetries = opts.MaxRetries,
                connection = new
                {
                    maxConnectionsPerServer = opts.Connection.MaxConnectionsPerServer,
                    pooledConnectionLifetimeMinutes = opts.Connection.PooledConnectionLifetimeMinutes,
                    pooledConnectionIdleTimeoutMinutes = opts.Connection.PooledConnectionIdleTimeoutMinutes,
                    enableHttp2 = opts.Connection.EnableHttp2,
                    enableHttp3 = opts.Connection.EnableHttp3,
                    enableMultipleHttp2Connections = opts.Connection.EnableMultipleHttp2Connections,
                },
                resilience = new
                {
                    circuitBreakerFailureThreshold = opts.Resilience.CircuitBreakerFailureThreshold,
                    circuitBreakerSamplingSeconds = opts.Resilience.CircuitBreakerSamplingSeconds,
                    circuitBreakerBreakSeconds = opts.Resilience.CircuitBreakerBreakSeconds,
                    hedgeAfterMs = opts.Resilience.HedgeAfterMs,
                },
                sdkGeneratedAgainstVersion = PolarClient.GeneratedAgainstVersion,
            });
        })
        .WithName("GetDiagnosticsConfig")
        .WithSummary("Returns the current PolarSharp configuration summary (masked token, mode, features).")
        .WithDescription("For development and staging use only. Never expose this endpoint in production without authentication.");

        group.MapGet("/health-check", async (
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthCheckService,
            CancellationToken ct) =>
        {
            var report = await healthCheckService.CheckHealthAsync(
                r => r.Tags.Contains("polar"),
                ct);

            return Results.Ok(new
            {
                status = report.Status.ToString(),
                entries = report.Entries.ToDictionary(
                    e => e.Key,
                    e => new
                    {
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        duration = e.Value.Duration.TotalMilliseconds,
                        exception = e.Value.Exception?.Message,
                    }),
                totalDuration = report.TotalDuration.TotalMilliseconds,
            });
        })
        .WithName("GetPolarHealthCheck")
        .WithSummary("Runs the PolarSharp health check and returns the result.");

        return app;
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "(not set)";

        if (token.Length <= 8)
            return "***";

        var prefix = token.Length > 12 ? token[..12] : token[..4];
        var suffix = token[^4..];
        return $"{prefix}...{suffix}";
    }
}
