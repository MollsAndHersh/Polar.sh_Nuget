using System.Net.Http.Headers;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using PolarSharp.Concurrency;

namespace PolarSharp.MultiTenant;

/// <summary>
/// Creates and caches one <see cref="PolarClient"/> per tenant with full per-tenant bulkhead
/// isolation: each tenant gets a dedicated connection pool, circuit breaker, retry budget,
/// and bearer token — completely independent from every other tenant.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Bulkhead isolation:</strong> Each tenant's <see cref="PolarClient"/> is backed by
/// its own <see cref="SocketsHttpHandler"/> (own connection pool) wrapped by a
/// <see cref="TenantResilienceDelegatingHandler"/> carrying a dedicated
/// <see cref="ResiliencePipeline{T}"/>. One tenant tripping its circuit breaker has zero
/// effect on any other tenant's latency or availability.
/// </para>
/// <para>
/// <strong>Race-free initialization:</strong> Uses <see cref="LazyConcurrentDictionary{TKey,TValue}"/>
/// with <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> to guarantee the per-tenant
/// factory delegate runs at most once per tenant, even under high concurrent contention.
/// </para>
/// <para>
/// Registered as a <c>Singleton</c> by
/// <see cref="Extensions.MultiTenantBuilderExtensions.AddPolarMultiTenant"/>.
/// All per-tenant resources are released during application shutdown via
/// <see cref="IAsyncDisposable"/>.
/// </para>
/// </remarks>
internal sealed class MultiTenantPolarClientFactory(
    IMultiTenantContextAccessor<PolarTenantInfo> tenantContextAccessor,
    ILogger<MultiTenantPolarClientFactory> logger)
    : IMultiTenantPolarClientFactory, IAsyncDisposable
{
    private readonly LazyConcurrentDictionary<string, TenantEntry> _entries = new();

    /// <inheritdoc/>
    public PolarClient GetClientForCurrentTenant()
    {
        var tenantInfo = tenantContextAccessor.MultiTenantContext?.TenantInfo;

        if (tenantInfo is null)
            throw new InvalidOperationException(
                "No tenant has been resolved for the current request. " +
                "Ensure UseMultiTenancy() (via app.UsePolarInfrastructure()) is registered " +
                "in the middleware pipeline before the endpoint handler, and that the request " +
                "carries a recognized tenant identifier.");

        if (string.IsNullOrWhiteSpace(tenantInfo.Id))
            throw new InvalidOperationException(
                $"The resolved tenant '{tenantInfo.Identifier}' has no Id set. " +
                "Ensure every PolarTenantInfo entry has a non-empty Id.");

        return _entries.GetOrAdd(tenantInfo.Id, id => CreateEntry(id, tenantInfo)).Client;
    }

    private TenantEntry CreateEntry(string tenantId, PolarTenantInfo tenantInfo)
    {
        logger.LogInformation(
            "PolarSharp MultiTenant: creating isolated Polar client for tenant '{TenantId}' " +
            "({TenantName}) targeting {Server}.",
            tenantId, tenantInfo.Name ?? tenantId, tenantInfo.Server);

        var pipeline = BuildTenantResiliencePipeline(tenantId);

        var socketsHandler = new SocketsHttpHandler
        {
            // Own connection pool per tenant — DNS rotation + memory reclamation.
            PooledConnectionLifetime    = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer     = 100,
            EnableMultipleHttp2Connections = true,
            // SSRF defense: never follow redirects from the API.
            AllowAutoRedirect = false,
        };

        var resilienceHandler = new TenantResilienceDelegatingHandler(pipeline)
        {
            InnerHandler = socketsHandler,
        };

        var httpClient = new HttpClient(resilienceHandler)
        {
            BaseAddress = new Uri(GetBaseUrl(tenantInfo.Server)),
        };
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantInfo.PolarAccessToken);

        return new TenantEntry(new PolarClient(httpClient), httpClient);
    }

    /// <summary>
    /// Builds a per-tenant <see cref="ResiliencePipeline{T}"/> with independent circuit-breaker,
    /// retry, and timeout state.
    /// </summary>
    private static ResiliencePipeline<HttpResponseMessage> BuildTenantResiliencePipeline(string tenantId)
        => new ResiliencePipelineBuilder<HttpResponseMessage>()
            // Retry: 3 attempts, exponential back-off with jitter; handles transient HTTP errors.
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType      = DelayBackoffType.Exponential,
                UseJitter        = true,
                Delay            = TimeSpan.FromMilliseconds(200),
                Name             = $"polar.tenant.{tenantId}.retry",
            })
            // Circuit breaker: opens after 50 % failures over 5+ requests in 30 s; breaks 15 s.
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio      = 0.5,
                MinimumThroughput = 5,
                SamplingDuration  = TimeSpan.FromSeconds(30),
                BreakDuration     = TimeSpan.FromSeconds(15),
                Name              = $"polar.tenant.{tenantId}.cb",
            })
            // Per-attempt timeout.
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();

    private static string GetBaseUrl(PolarServer server) => server switch
    {
        PolarServer.Sandbox    => "https://sandbox-api.polar.sh",
        PolarServer.Production => "https://api.polar.sh",
        _                      => "https://api.polar.sh",
    };

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        // Snapshot the currently-created entries (skips unevaluated lazy slots) and
        // dispose their HttpClients — which in turn disposes the handler chain
        // (TenantResilienceDelegatingHandler + SocketsHttpHandler).
        var entries = _entries.Values.ToArray();
        _entries.Clear();

        foreach (var entry in entries)
            entry.HttpClient.Dispose();

        return ValueTask.CompletedTask;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>Bundles a <see cref="PolarClient"/> with the <see cref="HttpClient"/> that backs it.</summary>
    private sealed record TenantEntry(PolarClient Client, HttpClient HttpClient);
}
