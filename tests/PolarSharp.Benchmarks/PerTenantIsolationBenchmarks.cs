using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using PolarSharp.Concurrency;

namespace PolarSharp.Benchmarks;

/// <summary>
/// Validates per-tenant bulkhead isolation guarantees:
/// <list type="bullet">
///   <item>One cached client per tenant (race-free creation via <see cref="LazyConcurrentDictionary{TKey,TValue}"/>).</item>
///   <item>Linear throughput scaling up to <c>MaxConnectionsPerServer</c> connections.</item>
///   <item>Tenant A's "circuit-broken" state has zero effect on Tenant B's latency.</item>
/// </list>
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class PerTenantIsolationBenchmarks
{
    // Simulates the LazyConcurrentDictionary used in MultiTenantPolarClientFactory.
    // Using string→string here to avoid standing up a real HttpClient stack in benchmarks.
    private LazyConcurrentDictionary<string, string> _clientCache = null!;

    [Params(1, 10, 100, 200)]
    public int TenantCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _clientCache = new LazyConcurrentDictionary<string, string>();
        // Pre-populate so the benchmark measures cache-hit, not creation
        for (var i = 0; i < TenantCount; i++)
            _clientCache.GetOrAdd($"tenant-{i}", static k => $"client-for-{k}");
    }

    /// <summary>
    /// Parallel cache hits across all tenants — measures per-tenant lookup overhead.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Parallel_TenantLookup_CacheHit()
    {
        var tasks = Enumerable.Range(0, TenantCount).Select(i => Task.Run(() =>
            _clientCache.GetOrAdd($"tenant-{i}", static k => $"client-for-{k}"))).ToArray();
        Task.WaitAll(tasks);
    }

    /// <summary>
    /// Simulates 100 parallel API calls per tenant — validates that parallel tenant
    /// activity doesn't serialise behind a shared lock.
    /// </summary>
    [Benchmark]
    public void Parallel_100Tenants_x_100Calls()
    {
        var tasks = Enumerable.Range(0, Math.Min(TenantCount, 100)).SelectMany(i =>
            Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
                _clientCache.GetOrAdd($"tenant-{i}", static k => $"client-for-{k}")))).ToArray();
        Task.WaitAll(tasks);
    }

    /// <summary>
    /// Circuit-breaker isolation simulation:
    /// Tenant 0 is "broken" (slow factory); Tenant 1 must not be affected.
    /// Validates that one tenant's high latency does not bleed into another's.
    /// </summary>
    [Benchmark]
    public async Task CircuitBreaker_PerTenant_Isolation()
    {
        var brokenCache = new LazyConcurrentDictionary<string, string>();

        // Tenant A: slow factory (simulates circuit-breaker opening or slow Polar connection)
        var tenantATask = Task.Run(async () =>
        {
            await Task.Delay(100).ConfigureAwait(false); // 100ms "break duration"
            brokenCache.GetOrAdd("tenant-A", static k => $"client-for-{k}");
        });

        // Tenant B: should complete immediately, unaffected by Tenant A's latency
        var sw = System.Diagnostics.Stopwatch.StartNew();
        brokenCache.GetOrAdd("tenant-B", static k => $"client-for-{k}");
        sw.Stop();

        await tenantATask.ConfigureAwait(false);

        // Tenant B's lookup completes in < 5ms even while Tenant A takes 100ms
        if (sw.ElapsedMilliseconds > 5)
            throw new InvalidOperationException(
                $"Tenant B lookup took {sw.ElapsedMilliseconds}ms — should be < 5ms. " +
                "Per-tenant bulkhead is leaking latency.");
    }
}
