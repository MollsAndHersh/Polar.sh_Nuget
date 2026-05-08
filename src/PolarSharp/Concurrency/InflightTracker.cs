using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PolarSharp.Concurrency;

/// <summary>
/// Tracks the number of in-flight Polar API requests via an <see cref="UpDownCounter{T}"/>.
/// </summary>
/// <remarks>
/// Exposed as the <c>polar.requests.inflight</c> metric. Watching this gauge against
/// <c>PolarSharp:Connection:MaxConnectionsPerServer</c> gives operators advance warning
/// of connection pool saturation before requests start timing out.
/// <para>
/// Thread-safe: <see cref="UpDownCounter{T}"/> operations are atomic.
/// </para>
/// </remarks>
internal sealed class InflightTracker
{
    private readonly UpDownCounter<long> _gauge;

    /// <summary>
    /// Initializes the tracker using the provided counter.
    /// </summary>
    /// <param name="gauge">The counter to increment/decrement.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="gauge"/> is <see langword="null"/>.</exception>
    public InflightTracker(UpDownCounter<long> gauge)
    {
        ArgumentNullException.ThrowIfNull(gauge);
        _gauge = gauge;
    }

    /// <summary>
    /// Increments the inflight count for the given <paramref name="tenantId"/> and <paramref name="resource"/>,
    /// and returns a disposable scope that decrements the count on disposal.
    /// </summary>
    /// <param name="tenantId">The tenant identifier tag (empty string for single-tenant deployments).</param>
    /// <param name="resource">The Polar resource area being called (e.g., <c>"orders"</c>).</param>
    /// <returns>
    /// An <see cref="IDisposable"/> that decrements the inflight count when disposed.
    /// Use in a <c>using</c> declaration around the outbound HTTP call.
    /// </returns>
    public IDisposable TrackInflight(string tenantId, string resource)
    {
        var tags = new TagList
        {
            { "polar.tenant_id", tenantId },
            { "polar.resource",  resource }
        };
        _gauge.Add(1, tags);
        return new InflightScope(_gauge, tags);
    }

    private sealed class InflightScope(UpDownCounter<long> gauge, TagList tags) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            // Guard against double-dispose (e.g. from using + finally blocks)
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                gauge.Add(-1, tags);
        }
    }
}
