namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;

/// <summary>
/// Provider-agnostic EF Core event store for the PolarSharp prepaid wallet.
/// </summary>
/// <remarks>
/// <para>
/// The schema (defined in Phase 20.x):
/// </para>
/// <code>
/// wallet_events
///   id, wallet_id, sequence_no, event_type, event_payload_json,
///   idempotency_key, occurred_at, actor_user_id, source_ip_hash
///
/// wallet_snapshots
///   wallet_id, sequence_no, balance, frozen, closed,
///   snapshot_payload_json, taken_at
/// </code>
/// <para>
/// Optimistic concurrency via unique <c>(wallet_id, sequence_no)</c> index; second writer
/// on a stale read fails with <c>WalletConcurrencyConflictException</c>; MediatR retry
/// behavior recovers.
/// </para>
/// </remarks>
public interface IWalletEventStore
{
    /// <summary>Appends new events to a wallet stream.</summary>
    /// <remarks>Phase 20.x: actual signature + semantics.</remarks>
    Task AppendAsync(Guid walletId, IReadOnlyList<object> events, CancellationToken ct = default);

    /// <summary>Loads the event stream + snapshots for a wallet.</summary>
    Task<IReadOnlyList<object>> LoadAsync(Guid walletId, CancellationToken ct = default);
}
