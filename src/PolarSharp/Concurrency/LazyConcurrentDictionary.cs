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
/// This class uses a two-dictionary design with per-key guard objects: values are stored in one
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> while lightweight sentinel objects provide
/// per-key mutual exclusion, guaranteeing the factory executes exactly once per key.
/// </para>
/// <para>Thread-safe: all operations use lock-free concurrent primitives or per-key locks.</para>
/// </remarks>
internal sealed class LazyConcurrentDictionary<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly ConcurrentDictionary<TKey, TValue> _values = new();
    private readonly ConcurrentDictionary<TKey, object> _guards = new();

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

        // Fast path — already created.
        if (_values.TryGetValue(key, out var existing))
            return existing;

        // Slow path — acquire a per-key guard to ensure single factory execution.
        // Multiple threads may create guard objects; all but one are discarded (cheap).
        var guard = _guards.GetOrAdd(key, static _ => new object());
        lock (guard)
        {
            // Double-check after acquiring the lock.
            if (_values.TryGetValue(key, out existing))
                return existing;

            var created = factory(key);
            _values.TryAdd(key, created);
            return created;
        }
    }

    /// <summary>
    /// Gets all values currently stored in the dictionary.
    /// </summary>
    public IEnumerable<TValue> Values => _values.Values;

    /// <summary>Gets the number of keys in the dictionary.</summary>
    public int Count => _values.Count;

    /// <summary>
    /// Removes all entries from the dictionary.
    /// </summary>
    /// <remarks>Does not dispose the values — the caller is responsible for disposal.</remarks>
    public void Clear()
    {
        _values.Clear();
        _guards.Clear();
    }

    /// <summary>
    /// Attempts to remove and return the value for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <param name="value">
    /// When this method returns <see langword="true"/>, contains the removed value;
    /// otherwise the default for <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><see langword="true"/> if the key was found and removed; otherwise <see langword="false"/>.</returns>
    public bool TryRemove(TKey key, out TValue? value) => _values.TryRemove(key, out value);
}
