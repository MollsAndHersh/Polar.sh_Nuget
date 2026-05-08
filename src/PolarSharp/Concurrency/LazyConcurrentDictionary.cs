using System.Collections.Concurrent;

namespace PolarSharp.Concurrency;

/// <summary>
/// A thread-safe dictionary that guarantees the value factory is invoked at most once per key,
/// even under high concurrent contention.
/// </summary>
/// <typeparam name="TKey">The type of keys. Must be non-null.</typeparam>
/// <typeparam name="TValue">The type of values. Must be a reference type.</typeparam>
/// <remarks>
/// <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/> does NOT
/// guarantee the factory delegate runs only once per key — multiple threads racing for the same
/// missing key may each invoke the factory, with all but one result discarded. For expensive
/// factories (e.g., creating a <c>PolarClient</c> with its own <see cref="HttpClient"/> and
/// resilience pipeline), this waste — and potential resource leak — is unacceptable.
/// <para>
/// This class wraps each value in <see cref="Lazy{T}"/> with
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>, which uses double-checked locking
/// to guarantee a single factory execution per key regardless of concurrent access patterns.
/// </para>
/// <para>Thread-safe: all operations use lock-free concurrent primitives.</para>
/// </remarks>
internal sealed class LazyConcurrentDictionary<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly ConcurrentDictionary<TKey, Lazy<TValue>> _inner = new();

    /// <summary>
    /// Returns the value for <paramref name="key"/>, creating it via <paramref name="factory"/>
    /// if it does not exist. The factory is guaranteed to run at most once per key.
    /// </summary>
    /// <param name="key">The key to look up or create a value for.</param>
    /// <param name="factory">
    /// A function that creates the value for a new key. Guaranteed to run at most once per key
    /// under any concurrency level.
    /// </param>
    /// <returns>The existing or newly created value for <paramref name="key"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> or <paramref name="factory"/> is <see langword="null"/>.
    /// </exception>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return _inner
            .GetOrAdd(
                key,
                k => new Lazy<TValue>(
                    () => factory(k),
                    LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;
    }

    /// <summary>
    /// Gets all values whose <see cref="Lazy{T}"/> has already been evaluated (i.e., whose
    /// factory has already run). Does not trigger factory execution.
    /// </summary>
    public IEnumerable<TValue> Values =>
        _inner.Values
            .Where(lazy => lazy.IsValueCreated)
            .Select(lazy => lazy.Value);

    /// <summary>Gets the number of keys in the dictionary (including unevaluated lazy entries).</summary>
    public int Count => _inner.Count;

    /// <summary>
    /// Removes all entries from the dictionary.
    /// </summary>
    /// <remarks>Does not dispose the values — the caller is responsible for disposal.</remarks>
    public void Clear() => _inner.Clear();

    /// <summary>
    /// Attempts to remove and return the value for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <param name="value">
    /// When this method returns <see langword="true"/>, contains the removed value;
    /// otherwise the default for <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><see langword="true"/> if the key was found and removed; otherwise <see langword="false"/>.</returns>
    public bool TryRemove(TKey key, out TValue? value)
    {
        if (_inner.TryRemove(key, out var lazy) && lazy.IsValueCreated)
        {
            value = lazy.Value;
            return true;
        }
        value = default;
        return false;
    }
}
