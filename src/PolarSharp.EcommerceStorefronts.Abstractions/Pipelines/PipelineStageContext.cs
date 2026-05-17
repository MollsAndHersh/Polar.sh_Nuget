namespace PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

/// <summary>
/// Per-pipeline-invocation context threaded through every stage. Carries the ambient
/// scope information stages need but should not have to re-resolve from DI on every
/// call (tenant id, customer id, idempotency key, correlation id).
/// </summary>
/// <remarks>
/// The context is intentionally immutable and small — stages that need broader scope
/// state (e.g. an EF Core <c>DbContext</c>) should resolve it from DI using the
/// scoped service provider their orchestrator was constructed from.
/// </remarks>
public sealed record PipelineStageContext
{
    /// <summary>The tenant scope; <see cref="StorefrontOption{T}.None"/> in single-tenant mode.</summary>
    public required StorefrontOption<Guid> TenantId { get; init; }

    /// <summary>The authenticated customer; <see cref="StorefrontOption{T}.None"/> for guest checkouts.</summary>
    public required StorefrontOption<Guid> CustomerId { get; init; }

    /// <summary>
    /// Stable correlation id that ties together every log event + emitted pipeline
    /// event for this invocation. Typically the order / subscription / refund id.
    /// </summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>
    /// Optional idempotency token. When supplied, stages that perform external
    /// side-effects (payment capture, fulfillment) must include this value in their
    /// upstream calls so retries do not double-charge.
    /// </summary>
    public StorefrontOption<string> IdempotencyKey { get; init; }

    /// <summary>UTC timestamp the pipeline started; provided so stage-emitted diagnostics share a clock.</summary>
    public required DateTimeOffset StartedAt { get; init; }
}
