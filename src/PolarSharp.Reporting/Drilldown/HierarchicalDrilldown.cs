namespace PolarSharp.Reporting.Drilldown;

/// <summary>
/// Generic page-of-rows envelope returned by every drilldown query — Customers, Orders, etc.
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>The page of rows.</summary>
    public required IReadOnlyList<T> Rows { get; init; }
    /// <summary>Total row count across all pages (server-side count).</summary>
    public required int TotalCount { get; init; }
    /// <summary>0-based page index this response represents.</summary>
    public required int Page { get; init; }
    /// <summary>Rows-per-page used to compute the page.</summary>
    public required int PageSize { get; init; }
    /// <summary>Convenience flag — true when more pages exist after this one.</summary>
    public bool HasMore => (Page + 1) * PageSize < TotalCount;
}

/// <summary>Shared paging / sort / filter knobs for drilldown queries.</summary>
public abstract record DrilldownQueryBase
{
    /// <summary>0-based page index. Default 0.</summary>
    public int Page { get; init; }
    /// <summary>Rows per page. Default 50; the server caps at 500.</summary>
    public int PageSize { get; init; } = 50;
    /// <summary>Optional sort field. Each query has its own allow-list — unknown values fall back to the default sort.</summary>
    public string? SortBy { get; init; }
    /// <summary>True for newest-first / largest-first sort. Default true.</summary>
    public bool SortDescending { get; init; } = true;
}

/// <summary>Request shape for <see cref="IPolarReportingClient.ListCustomersAsync"/>.</summary>
public sealed record CustomerListRequest : DrilldownQueryBase
{
    /// <summary>Optional substring filter on email or name. Server-side ILIKE.</summary>
    public string? SearchTerm { get; init; }
    /// <summary>Lower bound on customer creation date.</summary>
    public DateTimeOffset? CreatedAfter { get; init; }
    /// <summary>Upper bound on customer creation date.</summary>
    public DateTimeOffset? CreatedBefore { get; init; }
}

/// <summary>Request shape for <see cref="IPolarReportingClient.ListOrdersForCustomerAsync"/>.</summary>
public sealed record OrderListRequest : DrilldownQueryBase
{
    /// <summary>Lower bound on order creation date.</summary>
    public DateTimeOffset? CreatedAfter { get; init; }
    /// <summary>Upper bound on order creation date.</summary>
    public DateTimeOffset? CreatedBefore { get; init; }
    /// <summary>Optional Polar wire-format status filter (<c>pending</c> | <c>paid</c> | <c>refunded</c> | <c>partially_refunded</c> | <c>void</c>).</summary>
    public string? Status { get; init; }
}

/// <summary>Top-level row for the "all customers" grid.</summary>
public sealed record CustomerListRow
{
    /// <summary>Polar customer id.</summary>
    public required string CustomerId { get; init; }
    /// <summary>Customer email.</summary>
    public required string Email { get; init; }
    /// <summary>Customer name. May be <see langword="null"/> for anonymous / guest customers.</summary>
    public string? Name { get; init; }
    /// <summary>Pre-aggregated count of orders this customer has placed.</summary>
    public required int OrderCount { get; init; }
    /// <summary>Pre-aggregated sum of net revenue (gross − refunded) across the customer's orders, in minor units.</summary>
    public required long LifetimeValue { get; init; }
    /// <summary>ISO 4217 currency code for <see cref="LifetimeValue"/>.</summary>
    public required string Currency { get; init; }
    /// <summary>UTC of the customer's first order. <see langword="null"/> when they've never ordered.</summary>
    public DateTimeOffset? FirstOrderAt { get; init; }
    /// <summary>UTC of the customer's most recent order.</summary>
    public DateTimeOffset? LastOrderAt { get; init; }
    /// <summary>UTC when the customer record was created in Polar.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Mid-level row — appears when an operator opens one customer row.</summary>
public sealed record OrderSummaryRow
{
    /// <summary>Polar order id.</summary>
    public required string OrderId { get; init; }
    /// <summary>Human-readable order number.</summary>
    public required string OrderNumber { get; init; }
    /// <summary>Polar wire-format status.</summary>
    public required string Status { get; init; }
    /// <summary>Total amount in minor units.</summary>
    public required long Amount { get; init; }
    /// <summary>Tax amount in minor units.</summary>
    public required long TaxAmount { get; init; }
    /// <summary>Refunded amount in minor units (0 when the order has not been refunded).</summary>
    public required long RefundedAmount { get; init; }
    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }
    /// <summary>Pre-aggregated count of line items on the order — drives whether the host shows a "view items" affordance.</summary>
    public required int LineItemCount { get; init; }
    /// <summary>URL to the hosted PDF invoice. May be <see langword="null"/> until Polar generates it.</summary>
    public string? InvoiceUrl { get; init; }
    /// <summary>UTC when the order was placed.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
    /// <summary>UTC when the order was fulfilled. <see langword="null"/> for pending orders.</summary>
    public DateTimeOffset? FulfilledAt { get; init; }
}

/// <summary>Bottom-level detail — appears when an operator opens one order row.</summary>
public sealed record OrderDrilldownDetail
{
    /// <summary>Polar order id.</summary>
    public required string OrderId { get; init; }
    /// <summary>Human-readable order number.</summary>
    public required string OrderNumber { get; init; }
    /// <summary>Polar customer id.</summary>
    public required string CustomerId { get; init; }
    /// <summary>Customer email (snapshotted at order time).</summary>
    public required string CustomerEmail { get; init; }
    /// <summary>Polar wire-format status.</summary>
    public required string Status { get; init; }
    /// <summary>Total amount.</summary>
    public required long Amount { get; init; }
    /// <summary>Tax amount.</summary>
    public required long TaxAmount { get; init; }
    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }
    /// <summary>Line items.</summary>
    public required IReadOnlyList<OrderLineItemRow> LineItems { get; init; }
    /// <summary>Refunds issued against this order.</summary>
    public IReadOnlyList<OrderRefundRow> Refunds { get; init; } = [];
    /// <summary>Benefit grants emitted by this order.</summary>
    public IReadOnlyList<BenefitGrantRow> BenefitGrants { get; init; } = [];
    /// <summary>URL to the hosted PDF invoice.</summary>
    public string? InvoiceUrl { get; init; }
    /// <summary>UTC when the order was placed.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
    /// <summary>UTC when the order was fulfilled.</summary>
    public DateTimeOffset? FulfilledAt { get; init; }
}

/// <summary>One line item on an order.</summary>
public sealed record OrderLineItemRow(
    string ProductId,
    string ProductName,
    string? PriceId,
    int Quantity,
    long UnitAmount,
    long LineTotal,
    long DiscountAmount,
    long TaxAmount);

/// <summary>One refund issued against an order.</summary>
public sealed record OrderRefundRow(
    string RefundId,
    long Amount,
    string Currency,
    string Reason,
    DateTimeOffset CreatedAt);

/// <summary>One benefit grant emitted by an order.</summary>
public sealed record BenefitGrantRow(
    string BenefitId,
    string BenefitName,
    string BenefitKind,
    bool IsGranted,
    DateTimeOffset? GrantedAt,
    DateTimeOffset? RevokedAt);
