namespace PolarSharp.Webhooks.Reconciliation;

/// <summary>
/// Configuration for the Polar webhook event reconciliation service.
/// </summary>
/// <remarks>
/// Bound from <c>PolarSharp:Webhooks:Reconciliation</c>. When configured, the
/// reconciliation <c>IHostedService</c> periodically calls <c>polar.Events.ListAsync</c>
/// with a timestamp checkpoint to replay any events that Polar delivered but the host
/// app missed (e.g., due to network partitions or downtime).
/// <para>
/// Replayed events flow through the same <c>IPolarWebhookDispatcher</c> pipeline as
/// live webhooks. Handler implementations must be idempotent — the same event may be
/// delivered more than once.
/// </para>
/// <example>
/// <code>
/// "PolarSharp": {
///   "Webhooks": {
///     "Reconciliation": {
///       "Enabled": true,
///       "IntervalMinutes": 15,
///       "MaxLookbackHours": 24,
///       "Storage": "File",
///       "FilePath": "/var/lib/polarsharp/checkpoint.json"
///     }
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public class PolarReconciliationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the reconciliation service is active.
    /// </summary>
    /// <value><see langword="true"/> (default when this section is present in config).</value>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how often the reconciliation check runs, in minutes.
    /// </summary>
    /// <value>Default: <c>15</c> minutes.</value>
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the maximum lookback window in hours.
    /// </summary>
    /// <value>
    /// Default: <c>24</c> hours. The reconciler never queries events older than
    /// this limit, even if the stored checkpoint is older.
    /// </value>
    public int MaxLookbackHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets the checkpoint storage backend.
    /// </summary>
    /// <value>Default: <see cref="ReconciliationStorage.File"/>.</value>
    public ReconciliationStorage Storage { get; set; } = ReconciliationStorage.File;

    /// <summary>
    /// Gets or sets the file path used when <see cref="Storage"/> is
    /// <see cref="ReconciliationStorage.File"/>.
    /// </summary>
    /// <value>
    /// Default: <c>polarsharp-checkpoint.json</c> in the current working directory.
    /// Use an absolute path in production deployments.
    /// </value>
    public string FilePath { get; set; } = "polarsharp-checkpoint.json";
}

/// <summary>
/// Specifies the storage backend for the reconciliation timestamp checkpoint.
/// </summary>
public enum ReconciliationStorage
{
    /// <summary>Persists the checkpoint to a local JSON file. Single-instance deployments only.</summary>
    File,

    /// <summary>Persists the checkpoint to Redis. Requires a registered <c>IConnectionMultiplexer</c>.</summary>
    Redis,

    /// <summary>Custom storage via <c>IReconciliationCheckpointStore</c>. Register your implementation before calling <c>AddPolarWebhookReconciliation</c>.</summary>
    Custom,
}
