namespace PolarSharp.Webhooks.Dedup;

/// <summary>
/// Configuration options for the in-memory webhook deduplication store.
/// </summary>
public sealed class PolarWebhookInMemoryDedupOptions
{
    /// <summary>
    /// Gets or sets the maximum number of webhook IDs retained in memory.
    /// Oldest entries are evicted when this limit is reached.
    /// </summary>
    /// <value>10,000 (default). Bound from <c>PolarSharp:Webhooks:InMemoryDedup:MaxEntries</c>.</value>
    public int MaxEntries { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets how long a webhook ID is retained before being pruned.
    /// Should be at least as long as Polar's retry window (~72 hours).
    /// </summary>
    /// <value>2 hours (default). Bound from <c>PolarSharp:Webhooks:InMemoryDedup:RetentionPeriod</c>.</value>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(2);
}
