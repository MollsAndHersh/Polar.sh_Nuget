using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Tests.Litestream;

/// <summary>
/// Tests for <see cref="LitestreamHealthCheck"/> — the ASP.NET Core health-check that
/// surfaces Litestream replication lag into the host's <c>/health</c> endpoint.
/// </summary>
/// <remarks>
/// <para>
/// The check issues an HTTP GET against <c>http://localhost:{MetricsPort}/metrics</c> and
/// parses the Prometheus-format response for <c>litestream_replica_lag_seconds</c>. Tests
/// exercise each of the documented status branches: master-off / check-disabled / unreachable /
/// healthy / degraded / parse-failure / timeout.
/// </para>
/// </remarks>
public sealed class LitestreamHealthCheckTests
{
    private static readonly HealthCheckContext EmptyContext = new()
    {
        Registration = new HealthCheckRegistration(
            name: "litestream",
            instance: NullHealthCheck.Instance,
            failureStatus: HealthStatus.Unhealthy,
            tags: Array.Empty<string>()),
    };

    // --- Master toggle / check toggle short-circuits ----------------------------------

    [Fact]
    public async Task CheckHealthAsync_returns_Healthy_when_UseLitestream_is_false()
    {
        var handler = new CapturingHttpMessageHandler();
        var opts = TestHelpers.FullyEnabledOptions();
        opts.UseLitestream = false;
        var sut = NewSut(handler, opts);

        var result = await sut.CheckHealthAsync(EmptyContext);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_Healthy_when_HealthCheckEnabled_is_false()
    {
        var handler = new CapturingHttpMessageHandler();
        var opts = TestHelpers.FullyEnabledOptions();
        opts.HealthCheckEnabled = false;
        var sut = NewSut(handler, opts);

        var result = await sut.CheckHealthAsync(EmptyContext);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    // --- Unreachable / non-2xx --------------------------------------------------------

    [Fact]
    public async Task CheckHealthAsync_returns_Unhealthy_when_metrics_endpoint_unreachable()
    {
        var handler = new CapturingHttpMessageHandler
        {
            Response = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("oops"),
            },
        };
        var sut = NewSut(handler, TestHelpers.FullyEnabledOptions());

        var result = await sut.CheckHealthAsync(EmptyContext);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Description);
        Assert.Contains("metrics", result.Description!, StringComparison.OrdinalIgnoreCase);
    }

    // --- Healthy / Degraded branches --------------------------------------------------

    [Fact]
    public async Task CheckHealthAsync_returns_Healthy_when_lag_is_below_threshold()
    {
        var handler = new CapturingHttpMessageHandler
        {
            Response = NewMetricsResponse(
                "# HELP litestream_replica_lag_seconds Replica lag in seconds.",
                "# TYPE litestream_replica_lag_seconds gauge",
                "litestream_replica_lag_seconds{db=\"/var/lib/polar/master_SaaS.db\",replica=\"s3\"} 5.2"),
        };
        var opts = TestHelpers.FullyEnabledOptions();
        opts.HealthCheckMaxLagSeconds = 30;
        var sut = NewSut(handler, opts);

        var result = await sut.CheckHealthAsync(EmptyContext);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(result.Data.ContainsKey("maxReplicaLagSeconds"));
        Assert.Equal(5.2d, (double)result.Data["maxReplicaLagSeconds"], precision: 5);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_Degraded_when_lag_exceeds_threshold()
    {
        var handler = new CapturingHttpMessageHandler
        {
            Response = NewMetricsResponse(
                "litestream_replica_lag_seconds{db=\"/var/lib/polar/master_SaaS.db\",replica=\"s3\"} 45.0"),
        };
        var opts = TestHelpers.FullyEnabledOptions();
        opts.HealthCheckMaxLagSeconds = 30;
        var sut = NewSut(handler, opts);

        var result = await sut.CheckHealthAsync(EmptyContext);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_parses_multiple_lag_metric_lines_and_uses_max()
    {
        var handler = new CapturingHttpMessageHandler
        {
            Response = NewMetricsResponse(
                "litestream_replica_lag_seconds{db=\"/db/master_SaaS.db\",replica=\"s3\"} 4.0",
                "litestream_replica_lag_seconds{db=\"/db/t1.db\",replica=\"s3\"} 27.0",
                "litestream_replica_lag_seconds{db=\"/db/t2.db\",replica=\"s3\"} 9.5"),
        };
        var opts = TestHelpers.FullyEnabledOptions();
        opts.HealthCheckMaxLagSeconds = 60;
        var sut = NewSut(handler, opts);

        var result = await sut.CheckHealthAsync(EmptyContext);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(27.0d, (double)result.Data["maxReplicaLagSeconds"], precision: 5);
    }

    [Fact]
    public async Task CheckHealthAsync_handles_malformed_prometheus_response_gracefully()
    {
        // No `litestream_replica_lag_seconds` metric lines at all — the parser returns null
        // (no samples observed) and the check reports Healthy with a "no samples yet" note.
        var handler = new CapturingHttpMessageHandler
        {
            Response = NewMetricsResponse(
                "this is not a valid prometheus response",
                "garbage garbage garbage",
                "<<random>>"),
        };
        var sut = NewSut(handler, TestHelpers.FullyEnabledOptions());

        var result = await sut.CheckHealthAsync(EmptyContext);

        // Parser returned no samples → reported Healthy with the no-samples message.
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(-1d, (double)result.Data["maxReplicaLagSeconds"], precision: 5);
    }

    // --- Timeout ----------------------------------------------------------------------

    [Fact]
    public async Task CheckHealthAsync_respects_5s_timeout()
    {
        var handler = new CapturingHttpMessageHandler
        {
            // Delay longer than the HttpClient timeout we set below.
            DelayBeforeResponse = TimeSpan.FromSeconds(10),
        };
        // Force the HttpClient timeout to 100 ms so the test does not actually wait 5 seconds.
        var sut = NewSut(handler, TestHelpers.FullyEnabledOptions(),
            httpClientTimeout: TimeSpan.FromMilliseconds(100));

        var result = await sut.CheckHealthAsync(EmptyContext);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    // --- Metrics-port URL composition -------------------------------------------------

    [Fact]
    public async Task CheckHealthAsync_uses_configured_MetricsPort()
    {
        var handler = new CapturingHttpMessageHandler
        {
            Response = NewMetricsResponse("litestream_replica_lag_seconds 0.1"),
        };
        var opts = TestHelpers.FullyEnabledOptions(metricsPort: 9999);
        var sut = NewSut(handler, opts);

        await sut.CheckHealthAsync(EmptyContext);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("http://localhost:9999/metrics", handler.LastRequest!.RequestUri!.ToString());
    }

    // --- Helpers ----------------------------------------------------------------------

    private static LitestreamHealthCheck NewSut(
        CapturingHttpMessageHandler handler,
        LitestreamOptions opts,
        TimeSpan? httpClientTimeout = null)
    {
        return new LitestreamHealthCheck(
            new TestHttpClientFactory(handler, httpClientTimeout),
            new StaticOptionsMonitor<LitestreamOptions>(opts),
            NullLogger<LitestreamHealthCheck>.Instance);
    }

    private static HttpResponseMessage NewMetricsResponse(params string[] lines)
    {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(string.Join('\n', lines)),
        };
    }

    /// <summary>
    /// Placeholder <see cref="IHealthCheck"/> used to satisfy the <see cref="HealthCheckRegistration"/>
    /// constructor when building a <see cref="HealthCheckContext"/> for the tests. Never invoked.
    /// </summary>
    private sealed class NullHealthCheck : IHealthCheck
    {
        public static readonly NullHealthCheck Instance = new();
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(HealthCheckResult.Healthy());
    }
}
