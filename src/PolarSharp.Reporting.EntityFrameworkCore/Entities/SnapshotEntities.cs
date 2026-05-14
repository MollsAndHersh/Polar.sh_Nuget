using PolarSharp.MultiTenant;

namespace PolarSharp.Reporting.EntityFrameworkCore.Entities;

/// <summary>Mirrors one row from Polar's <c>/v1/events/</c> stream into local SQL.</summary>
public sealed class ReportEventEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar event id (<c>evt_xxx</c>).</summary>
    public string PolarEventId { get; set; } = "";
    /// <summary>Polar event-type slug.</summary>
    public string Type { get; set; } = "";
    /// <summary>UTC when Polar recorded the event.</summary>
    public DateTimeOffset OccurredAt { get; set; }
    /// <summary>Raw event payload as JSON.</summary>
    public string? PayloadJson { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>
/// Mirrors a Polar order. Pre-aggregated columns (<see cref="LineItemCount"/>,
/// <see cref="RefundedAmount"/>) are updated on every snapshot tick so the drilldown's
/// mid-level grid loads without per-row roll-up queries.
/// </summary>
public sealed class ReportOrderEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar order id (<c>ord_xxx</c>).</summary>
    public string PolarOrderId { get; set; } = "";
    /// <summary>Human-readable order number.</summary>
    public string OrderNumber { get; set; } = "";
    /// <summary>Polar customer id.</summary>
    public string CustomerId { get; set; } = "";
    /// <summary>Polar wire-format status.</summary>
    public string Status { get; set; } = "";
    /// <summary>Total amount in minor units.</summary>
    public long Amount { get; set; }
    /// <summary>Tax amount in minor units.</summary>
    public long TaxAmount { get; set; }
    /// <summary>Pre-aggregated sum of refunds against this order.</summary>
    public long RefundedAmount { get; set; }
    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; set; } = "";
    /// <summary>Pre-aggregated count of line items.</summary>
    public int LineItemCount { get; set; }
    /// <summary>URL to the hosted PDF invoice.</summary>
    public string? InvoiceUrl { get; set; }
    /// <summary>UTC when the order was placed.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>UTC when the order was fulfilled.</summary>
    public DateTimeOffset? FulfilledAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>One line item from a mirrored order.</summary>
public sealed class ReportOrderLineItemEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>FK to <see cref="ReportOrderEntity.Id"/>.</summary>
    public Guid OrderId { get; set; }
    /// <summary>Polar product id.</summary>
    public string ProductId { get; set; } = "";
    /// <summary>Snapshotted product name at the time of purchase.</summary>
    public string ProductName { get; set; } = "";
    /// <summary>Polar price id, if known.</summary>
    public string? PriceId { get; set; }
    /// <summary>Quantity ordered.</summary>
    public int Quantity { get; set; }
    /// <summary>Per-unit price in minor units.</summary>
    public long UnitAmount { get; set; }
    /// <summary>Line total (quantity × unit) before discount and tax.</summary>
    public long LineTotal { get; set; }
    /// <summary>Discount applied at the line level.</summary>
    public long DiscountAmount { get; set; }
    /// <summary>Tax applied at the line level.</summary>
    public long TaxAmount { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>One refund issued against a mirrored order.</summary>
public sealed class ReportOrderRefundEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>FK to <see cref="ReportOrderEntity.Id"/>.</summary>
    public Guid OrderId { get; set; }
    /// <summary>Polar refund id.</summary>
    public string PolarRefundId { get; set; } = "";
    /// <summary>Refund amount in minor units.</summary>
    public long Amount { get; set; }
    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; set; } = "";
    /// <summary>Polar wire-format reason code.</summary>
    public string Reason { get; set; } = "";
    /// <summary>UTC when the refund was issued.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>Mirrors a Polar subscription.</summary>
public sealed class ReportSubscriptionEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar subscription id.</summary>
    public string PolarSubscriptionId { get; set; } = "";
    /// <summary>Polar customer id.</summary>
    public string CustomerId { get; set; } = "";
    /// <summary>Polar product id.</summary>
    public string ProductId { get; set; } = "";
    /// <summary>Polar wire-format status.</summary>
    public string Status { get; set; } = "";
    /// <summary>UTC when the subscription started.</summary>
    public DateTimeOffset StartedAt { get; set; }
    /// <summary>UTC when the subscription was cancelled. Null for active subscriptions.</summary>
    public DateTimeOffset? CanceledAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>
/// Mirrors a Polar customer with pre-aggregated columns (<see cref="OrderCount"/>,
/// <see cref="LifetimeValue"/>, <see cref="FirstOrderAt"/>, <see cref="LastOrderAt"/>) that
/// power the drilldown's top-level grid without per-row roll-up queries.
/// </summary>
public sealed class ReportCustomerEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar customer id.</summary>
    public string PolarCustomerId { get; set; } = "";
    /// <summary>Customer email.</summary>
    public string Email { get; set; } = "";
    /// <summary>Customer name.</summary>
    public string? Name { get; set; }
    /// <summary>Pre-aggregated order count.</summary>
    public int OrderCount { get; set; }
    /// <summary>Pre-aggregated lifetime value (sum of net revenue) in minor units.</summary>
    public long LifetimeValue { get; set; }
    /// <summary>Lifetime-value currency.</summary>
    public string Currency { get; set; } = "";
    /// <summary>UTC of the customer's first order.</summary>
    public DateTimeOffset? FirstOrderAt { get; set; }
    /// <summary>UTC of the customer's most recent order.</summary>
    public DateTimeOffset? LastOrderAt { get; set; }
    /// <summary>UTC when the customer record was created in Polar.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>One benefit grant emitted by a mirrored order.</summary>
public sealed class ReportBenefitGrantEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar grant id.</summary>
    public string PolarGrantId { get; set; } = "";
    /// <summary>Polar customer id.</summary>
    public string CustomerId { get; set; } = "";
    /// <summary>FK to <see cref="ReportOrderEntity.Id"/>.</summary>
    public Guid? OrderId { get; set; }
    /// <summary>Polar benefit id.</summary>
    public string BenefitId { get; set; } = "";
    /// <summary>Snapshotted benefit display name.</summary>
    public string BenefitName { get; set; } = "";
    /// <summary>Benefit-kind discriminator.</summary>
    public string BenefitKind { get; set; } = "";
    /// <summary>True when the grant is currently active.</summary>
    public bool IsGranted { get; set; }
    /// <summary>UTC when the grant was issued.</summary>
    public DateTimeOffset? GrantedAt { get; set; }
    /// <summary>UTC when the grant was revoked.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

// ── V20-005 Phase 1: 7 new resource snapshots ─────────────────────────────────────
//
// Each entity below mirrors a Polar.sh resource not previously snapshotted. They follow
// the same shape as the originals: surrogate Guid PK, ITenantOwned (TenantId stamped via
// global filter), IFakeDataAware (so DataSeeding-generated rows fall under the same
// fake-data filter), one column per Polar wire-format field we surface for reporting,
// plus a unique (TenantId, PolarXxxId) index that the upsert idempotency keys on.

/// <summary>Mirrors a Polar benefit definition from <c>/v1/benefits/</c> — the active benefit catalog per tenant.</summary>
public sealed class ReportBenefitEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar benefit id (<c>bnf_xxx</c>).</summary>
    public string PolarBenefitId { get; set; } = "";
    /// <summary>Display name shown to customers.</summary>
    public string Name { get; set; } = "";
    /// <summary>Benefit-kind discriminator (custom / discord / downloadables / feature_flag / github_repository / license_keys / meter_credit).</summary>
    public string Kind { get; set; } = "";
    /// <summary>Human-readable description.</summary>
    public string? Description { get; set; }
    /// <summary>True when the benefit is selectable / active.</summary>
    public bool IsActive { get; set; }
    /// <summary>UTC when Polar created the benefit.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>UTC of the most recent modification, if any.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>Mirrors a Polar discount definition from <c>/v1/discounts/</c> — for redemption / expiry-pipeline reports.</summary>
public sealed class ReportDiscountEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar discount id.</summary>
    public string PolarDiscountId { get; set; } = "";
    /// <summary>Discount display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Coupon code; null for automatic discounts.</summary>
    public string? Code { get; set; }
    /// <summary>Discount type discriminator (<c>fixed</c> / <c>percentage</c>).</summary>
    public string Type { get; set; } = "";
    /// <summary>Cents off when Type=fixed.</summary>
    public long? AmountOff { get; set; }
    /// <summary>Percent off when Type=percentage (0–100).</summary>
    public decimal? PercentOff { get; set; }
    /// <summary>ISO 4217 currency code, when applicable.</summary>
    public string? Currency { get; set; }
    /// <summary>Number of times redeemed so far (when Polar surfaces it).</summary>
    public int? RedemptionsSoFar { get; set; }
    /// <summary>Maximum allowed redemptions; null = unlimited.</summary>
    public int? MaxRedemptions { get; set; }
    /// <summary>UTC when the discount becomes valid.</summary>
    public DateTimeOffset? StartsAt { get; set; }
    /// <summary>UTC when the discount expires.</summary>
    public DateTimeOffset? EndsAt { get; set; }
    /// <summary>UTC when Polar created the discount.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>Mirrors a Polar checkout-link definition from <c>/v1/checkout-links/</c> — for conversion-funnel reports.</summary>
public sealed class ReportCheckoutLinkEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar checkout-link id.</summary>
    public string PolarCheckoutLinkId { get; set; } = "";
    /// <summary>Host-supplied label.</summary>
    public string Label { get; set; } = "";
    /// <summary>Comma-separated list of Polar product ids on this link.</summary>
    public string ProductIdsCsv { get; set; } = "";
    /// <summary>Public URL the host embeds.</summary>
    public string? Url { get; set; }
    /// <summary>Where Polar redirects after a successful purchase.</summary>
    public string? SuccessUrl { get; set; }
    /// <summary>True when discount codes are accepted on this link.</summary>
    public bool AllowDiscountCodes { get; set; }
    /// <summary>UTC when Polar created the link.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>Mirrors a Polar product from <c>/v1/products/</c> — for catalog-drift detection vs the host's local catalog.</summary>
public sealed class ReportProductEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar product id.</summary>
    public string PolarProductId { get; set; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Description.</summary>
    public string? Description { get; set; }
    /// <summary>True when the product is recurring (subscription).</summary>
    public bool IsRecurring { get; set; }
    /// <summary>Recurring interval (week / month / year etc.) when recurring.</summary>
    public string? RecurringInterval { get; set; }
    /// <summary>True when the product is archived in Polar.</summary>
    public bool IsArchived { get; set; }
    /// <summary>UTC when Polar created the product.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>UTC of the most recent modification, if any.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>Mirrors a Polar license key from <c>/v1/license-keys/</c> — for license-utilization / expiry-pipeline reports.</summary>
public sealed class ReportLicenseKeyEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar license-key id (NOT the raw key string — that's masked).</summary>
    public string PolarLicenseKeyId { get; set; } = "";
    /// <summary>Polar customer id this key belongs to.</summary>
    public string CustomerId { get; set; } = "";
    /// <summary>Polar benefit id this key was issued under.</summary>
    public string? BenefitId { get; set; }
    /// <summary>Display key (Polar's masked-for-UI representation; safe to surface in reports).</summary>
    public string? DisplayKey { get; set; }
    /// <summary>Status discriminator (<c>granted</c> / <c>revoked</c> / <c>disabled</c>).</summary>
    public string Status { get; set; } = "";
    /// <summary>Maximum allowed activations; null = unlimited.</summary>
    public int? LimitActivations { get; set; }
    /// <summary>Activations consumed so far.</summary>
    public int? ActivationsUsed { get; set; }
    /// <summary>UTC when the key expires; null = never.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>UTC when Polar issued the key.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>Mirrors a Polar usage-billing meter definition from <c>/v1/meters/</c>.</summary>
public sealed class ReportMeterEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar meter id.</summary>
    public string PolarMeterId { get; set; } = "";
    /// <summary>Display name (e.g. "API Calls", "GB Storage").</summary>
    public string Name { get; set; } = "";
    /// <summary>Polar's aggregation discriminator (sum / count / max / unique).</summary>
    public string AggregationKind { get; set; } = "";
    /// <summary>UTC when Polar created the meter.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>Mirrors a Polar per-customer per-meter tally from <c>/v1/customer-meters/</c>.</summary>
public sealed class ReportCustomerMeterEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }
    /// <inheritdoc/>
    public string TenantId { get; set; } = "";
    /// <summary>Polar customer-meter id.</summary>
    public string PolarCustomerMeterId { get; set; } = "";
    /// <summary>Polar customer id.</summary>
    public string CustomerId { get; set; } = "";
    /// <summary>Polar meter id this row tallies against.</summary>
    public string MeterId { get; set; } = "";
    /// <summary>Current consumed-units balance for the customer in the current billing period.</summary>
    public decimal ConsumedUnits { get; set; }
    /// <summary>Current credit balance (pre-provisioned units) for the customer.</summary>
    public decimal? CreditedUnits { get; set; }
    /// <summary>UTC when Polar created the customer-meter row.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>UTC of the most recent modification (typically last usage event).</summary>
    public DateTimeOffset? ModifiedAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}

/// <summary>Per-tenant per-resource checkpoint — the snapshot service resumes from here on each tick.</summary>
public sealed class ReportSnapshotCheckpointEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Composite key part 1: tenant.</summary>
    public string TenantId { get; set; } = "";
    /// <summary>Composite key part 2: which Polar resource this checkpoint tracks (e.g. <c>events</c>, <c>orders</c>).</summary>
    public string Resource { get; set; } = "";
    /// <summary>The Polar resource id of the last row we ingested.</summary>
    public string? LastPolarId { get; set; }
    /// <summary>UTC of the last snapshot run for this resource.</summary>
    public DateTimeOffset LastRunAt { get; set; }
    /// <inheritdoc/>
    public bool IsFakeData { get; set; }
}
