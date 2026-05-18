using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

/// <summary>
/// An <see cref="IHealthCheck"/> that surfaces the state of the optional Litestream
/// integration into the host's <c>/health</c> endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Decision flow on every <see cref="CheckHealthAsync"/>:
/// </para>
/// <list type="number">
///   <item>If <see cref="LitestreamOptions.UseLitestream"/> is <see langword="false"/>,
///   returns <see cref="HealthStatus.Healthy"/> with a "not enabled" message.</item>
///   <item>If <see cref="LitestreamOptions.HealthCheckEnabled"/> is <see langword="false"/>,
///   returns <see cref="HealthStatus.Healthy"/> with a "check disabled" message.</item>
///   <item>Otherwise issues an HTTP GET to <c>http://localhost:{MetricsPort}/metrics</c>
///   with a 5-second timeout. Connection refused or timeout returns
///   <see cref="HealthStatus.Unhealthy"/>.</item>
///   <item>Parses the Prometheus-format payload for <c>litestream_replica_lag_seconds</c>.
///   Max lag &gt; <see cref="LitestreamOptions.HealthCheckMaxLagSeconds"/> returns
///   <see cref="HealthStatus.Degraded"/>. Otherwise <see cref="HealthStatus.Healthy"/>.</item>
/// </list>
/// <para>
/// Registered via <see cref="LitestreamServiceCollectionExtensions.AddPolarSqliteLitestream"/>
/// with tags <c>polar-sql</c> and <c>polar-litestream</c>.
/// </para>
/// </remarks>
internal sealed class LitestreamHealthCheck : IHealthCheck
{
    /// <summary>The named <see cref="HttpClient"/> used to call the Litestream metrics endpoint.</summary>
    public const string HttpClientName = "PolarSharp.Litestream.HealthCheck";

    private const string LagMetricName = "litestream_replica_lag_seconds";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<LitestreamOptions> _options;
    private readonly ILogger<LitestreamHealthCheck> _logger;

    /// <summary>Initializes a new <see cref="LitestreamHealthCheck"/>.</summary>
    /// <param name="httpClientFactory">Factory for the metrics HTTP client.</param>
    /// <param name="options">Live options monitor for Litestream config.</param>
    /// <param name="logger">Logger.</param>
    public LitestreamHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<LitestreamOptions> options,
        ILogger<LitestreamHealthCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var opts = _options.CurrentValue;

        if (!opts.UseLitestream)
        {
            return HealthCheckResult.Healthy("Litestream not enabled.");
        }

        if (!opts.HealthCheckEnabled)
        {
            return HealthCheckResult.Healthy("Litestream health check disabled.");
        }

        var url = string.Create(CultureInfo.InvariantCulture, $"http://localhost:{opts.MetricsPort}/metrics");

        string payload;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(RequestTimeout);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client
                .GetAsync(new Uri(url), HttpCompletionOption.ResponseContentRead, cts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Litestream metrics endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return HealthCheckResult.Unhealthy(
                    $"Litestream metrics endpoint returned HTTP {(int)response.StatusCode}.");
            }

            payload = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Litestream metrics endpoint at {Url} unreachable; replication may not be running.",
                url);
            return HealthCheckResult.Unhealthy(
                "Litestream metrics endpoint unreachable; replication may not be running.",
                exception: ex);
        }

        var maxLag = ParseMaxReplicaLagSeconds(payload);

        var data = new Dictionary<string, object>
        {
            ["maxReplicaLagSeconds"] = maxLag ?? -1d,
            ["thresholdSeconds"] = opts.HealthCheckMaxLagSeconds,
        };

        if (maxLag is null)
        {
            return HealthCheckResult.Healthy(
                "Litestream metrics endpoint reachable; no replica lag samples reported yet.",
                data: data);
        }

        if (maxLag.Value > opts.HealthCheckMaxLagSeconds)
        {
            return HealthCheckResult.Degraded(
                $"Litestream replica lag {maxLag.Value:F1}s exceeds threshold {opts.HealthCheckMaxLagSeconds}s.",
                data: data);
        }

        return HealthCheckResult.Healthy(
            $"Litestream healthy; max replica lag {maxLag.Value:F1}s.",
            data: data);
    }

    /// <summary>
    /// Parses a Prometheus-format payload for the maximum sample of the
    /// <c>litestream_replica_lag_seconds</c> gauge. Returns null when no samples are found
    /// (a freshly-started Litestream may not have emitted any yet).
    /// </summary>
    /// <param name="payload">The raw Prometheus exposition response body.</param>
    /// <returns>The maximum observed lag in seconds, or null when no samples are present.</returns>
    private static double? ParseMaxReplicaLagSeconds(string payload)
    {
        double? max = null;
        foreach (var line in payload.Split('\n'))
        {
            var trimmed = line.AsSpan().TrimEnd('\r');
            if (trimmed.IsEmpty || trimmed[0] == '#')
            {
                continue;
            }
            if (!trimmed.StartsWith(LagMetricName.AsSpan(), StringComparison.Ordinal))
            {
                continue;
            }

            // Lines look like: litestream_replica_lag_seconds{db="...",replica="..."} 1.234
            var lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace < 0)
            {
                continue;
            }

            var valueSpan = trimmed[(lastSpace + 1)..];
            if (double.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (max is null || value > max.Value)
                {
                    max = value;
                }
            }
        }
        return max;
    }
}
