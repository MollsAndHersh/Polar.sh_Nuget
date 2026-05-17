namespace PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

/// <summary>
/// Data envelope carried through the refund-processing pipeline. Represents a single
/// refund attempt against an existing order.
/// </summary>
/// <remarks>
/// Stages populate amounts as they run. Money is stored in minor units (cents).
/// </remarks>
public sealed record RefundInProcess
{
    /// <summary>The refund identifier; assigned at pipeline entry and preserved across every stage.</summary>
    public required Guid RefundId { get; init; }

    /// <summary>The order the refund is being applied against.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>The customer receiving the refund.</summary>
    public required Guid CustomerId { get; init; }

    /// <summary>The tenant scope; <see cref="StorefrontOption{T}.None"/> in single-tenant mode.</summary>
    public required StorefrontOption<Guid> TenantId { get; init; }

    /// <summary>ISO 4217 currency code; locked at pipeline entry.</summary>
    public required string Currency { get; init; }

    /// <summary>Customer-requested refund amount in minor units.</summary>
    public required int RequestedAmountCents { get; init; }

    /// <summary>Final refund amount computed by <c>ComputeRefundAmountStage</c> in minor units.</summary>
    public int ApprovedAmountCents { get; init; }

    /// <summary>
    /// Reason key chosen by the merchant or customer (e.g. <c>"customer-changed-mind"</c>,
    /// <c>"defective-item"</c>). Stable identifier; the user-facing label is resolved by the UI.
    /// </summary>
    public required string ReasonKey { get; init; }

    /// <summary>Outcome flag downstream stages inspect to short-circuit on halt / failure.</summary>
    public PipelineOutcome Outcome { get; init; } = PipelineOutcome.Continue;

    /// <summary>Per-stage breadcrumb trail.</summary>
    public IReadOnlyList<StageDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Localizable resource key describing why the refund failed; populated when
    /// <see cref="Outcome"/> is <see cref="PipelineOutcome.Failed"/>.
    /// </summary>
    public StorefrontOption<string> FailureReasonKey { get; init; } = StorefrontOption<string>.None;
}
