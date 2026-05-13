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
