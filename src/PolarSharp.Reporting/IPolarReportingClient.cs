using PolarSharp;
using PolarSharp.Reporting.Drilldown;
using PolarSharp.Reporting.Reports;

namespace PolarSharp.Reporting;

/// <summary>
/// Per-tenant reporting client. Two flavours of method:
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description><strong>Aggregate / KPI</strong> — <see cref="GetTransactionsAsync"/>,
///   <see cref="GetSubscriptionsAsync"/>, <see cref="GetOrdersAsync"/>, etc. — return roll-up
///   metrics for dashboard tiles.</description></item>
///   <item><description><strong>Hierarchical drilldown</strong> —
///   <see cref="ListCustomersAsync"/> → <see cref="ListOrdersForCustomerAsync"/> →
///   <see cref="GetOrderDrilldownAsync"/> — three lazy methods designed to back
///   Telerik / MudBlazor hierarchical grids where rows expand on-demand. Each level is paged
///   independently; pre-aggregated columns (<c>OrderCount</c>, <c>LifetimeValue</c>,
///   <c>LineItemCount</c>) avoid per-row roll-up queries.</description></item>
/// </list>
/// <para>
/// Every method has a <c>*AsJsonAsync</c> variant returning the response as a pre-serialised
/// JSON string — saves the host a round-trip deserialisation when forwarding to a BI tool /
/// archive / SPA.
/// </para>
/// </remarks>
public interface IPolarReportingClient
{
    // ── Aggregate / KPI ────────────────────────────────────────────────

    /// <summary>Aggregate transaction roll-up over a date range.</summary>
    Task<Result<TransactionReport, PolarError>> GetTransactionsAsync(TransactionReportRequest request, CancellationToken ct = default);

    /// <summary>Subscription metrics — MRR / ARR / churn / cohort retention.</summary>
    Task<Result<SubscriptionReport, PolarError>> GetSubscriptionsAsync(SubscriptionReportRequest request, CancellationToken ct = default);

    /// <summary>Order counts + fulfilment latency.</summary>
    Task<Result<OrderReport, PolarError>> GetOrdersAsync(OrderReportRequest request, CancellationToken ct = default);

    /// <summary>Operational error / audit roll-up.</summary>
    Task<Result<ErrorAuditReport, PolarError>> GetErrorAuditAsync(ErrorAuditRequest request, CancellationToken ct = default);

    /// <summary>Customer roll-up + lifecycle segments.</summary>
    Task<Result<CustomerReport, PolarError>> GetCustomersAsync(CustomerReportRequest request, CancellationToken ct = default);

    /// <summary>Per-customer entitlement detail.</summary>
    Task<Result<CustomerEntitlementsReport, PolarError>> GetCustomerEntitlementsAsync(string customerId, CancellationToken ct = default);

    // ── JSON variants — return pre-serialised JSON, no DTO round-trip ─

    /// <summary>JSON variant of <see cref="GetTransactionsAsync"/>.</summary>
    Task<Result<string, PolarError>> GetTransactionsAsJsonAsync(TransactionReportRequest request, CancellationToken ct = default);

    /// <summary>JSON variant of <see cref="GetSubscriptionsAsync"/>.</summary>
    Task<Result<string, PolarError>> GetSubscriptionsAsJsonAsync(SubscriptionReportRequest request, CancellationToken ct = default);

    /// <summary>JSON variant of <see cref="GetOrdersAsync"/>.</summary>
    Task<Result<string, PolarError>> GetOrdersAsJsonAsync(OrderReportRequest request, CancellationToken ct = default);

    /// <summary>JSON variant of <see cref="GetErrorAuditAsync"/>.</summary>
    Task<Result<string, PolarError>> GetErrorAuditAsJsonAsync(ErrorAuditRequest request, CancellationToken ct = default);

    /// <summary>JSON variant of <see cref="GetCustomersAsync"/>.</summary>
    Task<Result<string, PolarError>> GetCustomersAsJsonAsync(CustomerReportRequest request, CancellationToken ct = default);

    // ── Hierarchical drilldown ─────────────────────────────────────────

    /// <summary>Top-level drilldown grid — every customer in the tenant, paged + filterable + with pre-aggregated <see cref="CustomerListRow.OrderCount"/> / <see cref="CustomerListRow.LifetimeValue"/>.</summary>
    Task<Result<PagedResult<CustomerListRow>, PolarError>> ListCustomersAsync(CustomerListRequest request, CancellationToken ct = default);

    /// <summary>Mid-level drilldown — orders for one customer, paged + filterable. Invoked when an operator opens a customer row.</summary>
    Task<Result<PagedResult<OrderSummaryRow>, PolarError>> ListOrdersForCustomerAsync(string customerId, OrderListRequest request, CancellationToken ct = default);

    /// <summary>Bottom-level drilldown — full detail for one order (line items + refunds + benefit grants). Invoked when an operator opens an order row.</summary>
    Task<Result<OrderDrilldownDetail, PolarError>> GetOrderDrilldownAsync(string orderId, CancellationToken ct = default);
}
