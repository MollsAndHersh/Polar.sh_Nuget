using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using PolarSharp.Concurrency;

namespace PolarSharp.Benchmarks;

/// <summary>
/// Verifies that <see cref="LazyConcurrentDictionary{TKey,TValue}"/> achieves
/// race-free single-factory invocation per key under high contention.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class LazyConcurrentDictionaryBenchmarks
{
    private LazyConcurrentDictionary<int, string> _dict = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dict = new LazyConcurrentDictionary<int, string>();
        // Pre-warm 50 keys
        for (var i = 0; i < 50; i++)
            _dict.GetOrAdd(i, static k => $"value-{k}");
    }

    /// <summary>
    /// Baseline: sequential hit on a pre-populated key — O(1) dictionary lookup.
    /// </summary>
    [Benchmark(Baseline = true)]
    public string? CacheHit() => _dict.GetOrAdd(0, static k => $"value-{k}");

    /// <summary>
    /// Single-thread cache miss — factory executes once, no contention.
    /// </summary>
    [Benchmark]
    public string? CacheMiss()
    {
        var fresh = new LazyConcurrentDictionary<int, string>();
        return fresh.GetOrAdd(1, static k => $"value-{k}");
    }

    /// <summary>
    /// High-contention race: 1 000 threads concurrently insert the same key.
    /// Factory must run exactly once. Validates the <see cref="Lazy{T}"/> guarantee.
    /// </summary>
    [Benchmark]
    public void Contention_1000Threads()
    {
        // Use a class wrapper (TValue : class constraint)
        var dict    = new LazyConcurrentDictionary<string, string>();
        var barrier = new Barrier(1_000);
        var factoryInvocations = 0;

        var tasks = Enumerable.Range(0, 1_000).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            dict.GetOrAdd("shared-key", _ =>
            {
                Interlocked.Increment(ref factoryInvocations);
                return "value";
            });
        })).ToArray();

        Task.WaitAll(tasks);

        if (factoryInvocations != 1)
            throw new InvalidOperationException(
                $"Factory ran {factoryInvocations} times — expected exactly 1.");
    }
}
