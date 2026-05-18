namespace PolarSharp.MultiTenant.Lifecycle;

/// <summary>Outcome of a tenant status change operation.</summary>
/// <remarks>
/// Returned from every <see cref="ITenantStatusService"/> mutator. Callers should branch on
/// <see cref="Success"/> first; on success, <see cref="WasIdempotentNoOp"/> distinguishes
/// "we changed the state and published the notification" from "the tenant was already in the
/// requested state — nothing happened, no notification fired".
/// </remarks>
public sealed record TenantStatusChangeResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    /// <value>
    /// <c>true</c> when the underlying store write succeeded (regardless of whether the
    /// MediatR notification dispatch later failed — notification failures are logged but do
    /// not flip this flag). <c>false</c> when the store write failed, the tenant was not found,
    /// or a pre-flight check rejected the operation (e.g., unverified email blocking suspension).
    /// </value>
    public required bool Success { get; init; }

    /// <summary>Gets the tenant's status before the operation.</summary>
    public required TenantStatus PreviousStatus { get; init; }

    /// <summary>Gets the tenant's status after the operation.</summary>
    /// <value>
    /// Equal to <see cref="PreviousStatus"/> when <see cref="WasIdempotentNoOp"/> is <c>true</c>
    /// or when <see cref="Success"/> is <c>false</c>.
    /// </value>
    public required TenantStatus NewStatus { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation was an idempotent no-op (the tenant was
    /// already in the requested status). When <c>true</c>, no MediatR notification was published.
    /// </summary>
    public required bool WasIdempotentNoOp { get; init; }

    /// <summary>Gets the UTC timestamp at which the operation completed.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Gets the human-readable failure reason when <see cref="Success"/> is <c>false</c>.</summary>
    /// <value>
    /// A short, actionable description suitable for logging and surfacing in admin UIs. <c>null</c>
    /// on success.
    /// </value>
    public string? FailureReason { get; init; }
}
