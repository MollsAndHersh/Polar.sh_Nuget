using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using CartEntity = PolarSharp.EcommerceStorefronts.Abstractions.Cart.Cart;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

/// <summary>
/// Data envelope carried through the order-processing pipeline. Each stage receives
/// one of these, may transform it via a <c>with</c>-expression, and yield-returns
/// the result (or zero, or multiple — though typically one).
/// </summary>
/// <remarks>
/// The record is intentionally immutable; stages produce a new copy rather than
/// mutating in place so the pipeline behaves predictably under cancellation and
/// concurrent observation. Money is stored in minor units (cents) as <see cref="int"/>
/// consistent with the rest of the storefront-core abstractions.
/// </remarks>
public sealed record OrderInProcess
{
    /// <summary>The order identifier; assigned at pipeline entry and preserved across every stage.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>The cart this order was initiated from (snapshot at pipeline entry).</summary>
    public required CartEntity Cart { get; init; }

    /// <summary>The authenticated customer; <see cref="StorefrontOption{T}.None"/> for guest checkouts.</summary>
    public required StorefrontOption<Guid> CustomerId { get; init; }

    /// <summary>The tenant scope; <see cref="StorefrontOption{T}.None"/> in single-tenant mode.</summary>
    public required StorefrontOption<Guid> TenantId { get; init; }

    /// <summary>ISO 4217 currency code; locked at pipeline entry.</summary>
    public required string Currency { get; init; }

    /// <summary>The current pipeline status; transitions as each stage completes.</summary>
    public required CheckoutStatus Status { get; init; }

    /// <summary>Outcome flag downstream stages inspect to short-circuit on halt / failure.</summary>
    public PipelineOutcome Outcome { get; init; } = PipelineOutcome.Continue;

    /// <summary>Optional shipping address (lifted from the cart at pipeline entry).</summary>
    public StorefrontOption<ShippingAddress> ShippingAddress { get; init; } = StorefrontOption<ShippingAddress>.None;

    /// <summary>Identifier of the shipping rate the customer selected, when applicable.</summary>
    public StorefrontOption<Guid> SelectedShippingRateId { get; init; } = StorefrontOption<Guid>.None;

    /// <summary>Order subtotal in minor units. Recomputed server-side by <c>ValidateLineItemsStage</c>.</summary>
    public int SubtotalCents { get; init; }

    /// <summary>Discount amount in minor units. Populated by <c>ApplyDiscountsStage</c>.</summary>
    public int DiscountCents { get; init; }

    /// <summary>Quoted tax in minor units. Populated by <c>QuoteTaxStage</c>.</summary>
    public int TaxCents { get; init; }

    /// <summary>Quoted shipping in minor units. Populated by <c>QuoteShippingStage</c>.</summary>
    public int ShippingCents { get; init; }

    /// <summary>
    /// Cumulative grand total in minor units. Computed as
    /// <c>Subtotal - Discount + Tax + Shipping</c> as the stages populate the inputs.
    /// </summary>
    public int TotalCents { get; init; }

    /// <summary>The discount code applied to the order, when any.</summary>
    public StorefrontOption<string> AppliedDiscountCode { get; init; } = StorefrontOption<string>.None;

    /// <summary>Per-stage breadcrumb trail; each stage appends one diagnostic.</summary>
    public IReadOnlyList<StageDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Localizable resource key describing why the order failed; populated when
    /// <see cref="Outcome"/> is <see cref="PipelineOutcome.Failed"/>.
    /// </summary>
    public StorefrontOption<string> FailureReasonKey { get; init; } = StorefrontOption<string>.None;
}
