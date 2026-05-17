namespace PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

/// <summary>
/// Data envelope carried through the subscription-billing pipeline. Represents a
/// single billing-cycle attempt — one invocation of the pipeline equals one cycle.
/// </summary>
/// <remarks>
/// Stages populate fields as they run. Money is stored in minor units (cents).
/// The status field is reused from the order pipeline's <c>CheckoutStatus</c>
/// enum so observability tooling does not need to switch on multiple status types.
/// </remarks>
public sealed record SubscriptionCycleInProcess
{
    /// <summary>The subscription identifier this cycle belongs to.</summary>
    public required Guid SubscriptionId { get; init; }

    /// <summary>The customer being billed.</summary>
    public required Guid CustomerId { get; init; }

    /// <summary>The tenant scope; <see cref="StorefrontOption{T}.None"/> in single-tenant mode.</summary>
    public required StorefrontOption<Guid> TenantId { get; init; }

    /// <summary>ISO 4217 currency code; locked at cycle entry.</summary>
    public required string Currency { get; init; }

    /// <summary>The cycle's billing-period start (UTC, inclusive).</summary>
    public required DateTimeOffset PeriodStart { get; init; }

    /// <summary>The cycle's billing-period end (UTC, exclusive).</summary>
    public required DateTimeOffset PeriodEnd { get; init; }

    /// <summary>The base subscription amount before proration / discounts, in minor units.</summary>
    public required int BaseAmountCents { get; init; }

    /// <summary>Discount applied to this cycle in minor units (coupon, loyalty, etc.).</summary>
    public int DiscountCents { get; init; }

    /// <summary>Positive (charge more) or negative (charge less) proration in minor units.</summary>
    public int ProrationCents { get; init; }

    /// <summary>Total to charge this cycle in minor units.</summary>
    public int TotalCents { get; init; }

    /// <summary>Outcome flag downstream stages inspect to short-circuit on halt / failure.</summary>
    public PipelineOutcome Outcome { get; init; } = PipelineOutcome.Continue;

    /// <summary>Per-stage breadcrumb trail.</summary>
    public IReadOnlyList<StageDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Localizable resource key describing why the cycle failed; populated when
    /// <see cref="Outcome"/> is <see cref="PipelineOutcome.Failed"/>.
    /// </summary>
    public StorefrontOption<string> FailureReasonKey { get; init; } = StorefrontOption<string>.None;
}
