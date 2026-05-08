namespace PolarSharp.Webhooks.Reconciliation;

/// <summary>
/// Persists and retrieves the timestamp checkpoint for the webhook reconciliation service.
/// </summary>
/// <remarks>
/// Implement this interface to use a custom checkpoint storage backend (e.g., Redis, SQL).
/// Register your implementation before calling <c>AddPolarWebhookReconciliation()</c>:
/// <code>
/// services.AddSingleton&lt;IReconciliationCheckpointStore, MyRedisCheckpointStore&gt;();
/// services
///     .AddPolarInfrastructure(configuration)
///     .AddPolarWebhooks()
///     .AddPolarWebhookReconciliation(opts => opts.Storage = ReconciliationStorage.Custom);
/// </code>
/// </remarks>
public interface IReconciliationCheckpointStore
{
    /// <summary>
    /// Retrieves the last processed timestamp checkpoint.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The timestamp of the last successfully processed event, or <see langword="null"/>
    /// if no checkpoint has been stored yet.
    /// </returns>
    Task<DateTimeOffset?> GetCheckpointAsync(CancellationToken ct = default);

    /// <summary>
    /// Stores the timestamp of the last successfully processed event.
    /// </summary>
    /// <param name="checkpoint">The timestamp to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetCheckpointAsync(DateTimeOffset checkpoint, CancellationToken ct = default);
}
