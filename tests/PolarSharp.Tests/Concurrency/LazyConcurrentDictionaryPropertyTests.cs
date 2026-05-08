using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using PolarSharp.Concurrency;

namespace PolarSharp.Tests.Concurrency;

/// <summary>
/// Property-based tests verifying that <see cref="LazyConcurrentDictionary{TKey,TValue}"/>
/// guarantees single factory invocation per key under arbitrary concurrency.
/// </summary>
public sealed class LazyConcurrentDictionaryPropertyTests
{
    /// <summary>
    /// For any key, the factory is invoked at most once regardless of how many threads race for it.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property Factory_InvokedAtMostOnce_PerKey(PositiveInt threadCount)
    {
        var count = Math.Min(threadCount.Get, 200); // cap at 200 to keep tests fast
        var dict = new LazyConcurrentDictionary<string, string>();
        var invocations = 0;

        var barrier = new Barrier(count);
        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            dict.GetOrAdd("shared-key", _ =>
            {
                Interlocked.Increment(ref invocations);
                return "value";
            });
        })).ToArray();

        Task.WaitAll(tasks);

        return Prop.Label(
            Prop.ToProperty(invocations == 1),
            $"Factory invoked {invocations} times with {count} threads — expected 1");
    }

    /// <summary>
    /// Different keys never share state — each key's factory is independent.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DifferentKeys_HaveIndependentFactories(NonEmptyString key1, NonEmptyString key2)
    {
        if (key1.Get == key2.Get)
            return Prop.ToProperty(true); // trivially true — can't test independence with same key

        var dict = new LazyConcurrentDictionary<string, string>();
        var value1 = dict.GetOrAdd(key1.Get, k => $"value-for-{k}");
        var value2 = dict.GetOrAdd(key2.Get, k => $"value-for-{k}");

        return Prop.Label(
            Prop.ToProperty(value1 != value2),
            $"Keys '{key1.Get}' and '{key2.Get}' should produce different values");
    }

    /// <summary>
    /// GetOrAdd always returns the same value for the same key (idempotent reads).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetOrAdd_IsDeterministic_ForSameKey(NonEmptyString key)
    {
        var dict = new LazyConcurrentDictionary<string, string>();
        var first  = dict.GetOrAdd(key.Get, k => $"value-for-{k}");
        var second = dict.GetOrAdd(key.Get, _ => "different-factory-that-should-not-run");

        return Prop.Label(
            Prop.ToProperty(first == second),
            $"First call returned '{first}', second call returned '{second}' — must be equal");
    }
}
