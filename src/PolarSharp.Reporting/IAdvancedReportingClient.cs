using PolarSharp.Reporting.Reports;

namespace PolarSharp.Reporting;

/// <summary>
/// Twelve additional reports added in v1.3.H — eight tenant-scoped reports for merchant
/// dashboards (revenue, top products / customers, churn cohorts, refund rate, AOV, LTV
/// distribution, currency mix) and four cross-tenant operator reports for AppMasterAdmin
/// dashboards (platform revenue, order volume per tenant, webhook health, tenant health).
/// </summary>
/// <remarks>
/// <para>
/// Kept on a separate interface from <see cref="IPolarReportingClient"/> so hosts can inject
/// just what they need and so the contract surface of each interface stays focused.
/// </para>
/// <para>
/// <strong>Tenant reports</strong> read through the catalog DbContext's global tenant query
/// filter — they automatically scope to the current tenant in scope. The operator reports
/// explicitly bypass the filter and are gated by <c>[RequireAppMasterAdmin]</c> at the
/// endpoint layer in the host application.
/// </para>
/// <para>
/// <strong>Read source</strong>: every report reads the snapshot tables populated by
/// <see cref="Snapshot.IReportSnapshotService"/>. With the snapshot service running on a
/// schedule, reports return tenant-scale data in milliseconds because the heavy aggregation
/// already happened during the snapshot pass.
/// </para>
/// </remarks>
public interface IAdvancedReportingClient
{
    // ── Tenant reports (auto-scoped via the global filter) ─────────────────

    /// <summary>Revenue / refunds / net-revenue time series.</summary>
    Task<Result<RevenueOverTimeReport, PolarError>> GetRevenueOverTimeAsync(RevenueOverTimeRequest request, CancellationToken ct = default);

    /// <summary>Top products by revenue and by units sold.</summary>
    Task<Result<TopProductsReport, PolarError>> GetTopProductsAsync(TopProductsRequest request, CancellationToken ct = default);

    /// <summary>Top customers by lifetime value (uses the pre-aggregated <c>LifetimeValue</c> column).</summary>
    Task<Result<TopCustomersReport, PolarError>> GetTopCustomersAsync(TopCustomersRequest request, CancellationToken ct = default);

    /// <summary>Subscription cohort retention — how many of a given month's new subscribers are still active over subsequent months.</summary>
    Task<Result<SubscriptionChurnCohortReport, PolarError>> GetSubscriptionChurnCohortAsync(SubscriptionChurnCohortRequest request, CancellationToken ct = default);

    /// <summary>Refund-rate time series — refund count and amount as a percentage of gross.</summary>
    Task<Result<RefundRateReport, PolarError>> GetRefundRateAsync(RefundRateRequest request, CancellationToken ct = default);

    /// <summary>Average-order-value time series.</summary>
    Task<Result<AverageOrderValueReport, PolarError>> GetAverageOrderValueAsync(AverageOrderValueRequest request, CancellationToken ct = default);

    /// <summary>Histogram of customers across configurable LTV buckets.</summary>
    Task<Result<CustomerLifetimeValueDistributionReport, PolarError>> GetCustomerLifetimeValueDistributionAsync(CustomerLifetimeValueDistributionRequest request, CancellationToken ct = default);

    /// <summary>Revenue and order count grouped by currency.</summary>
    Task<Result<CurrencyMixReport, PolarError>> GetCurrencyMixAsync(CurrencyMixRequest request, CancellationToken ct = default);

    // ── SaaS-operator reports (cross-tenant — gate with [RequireAppMasterAdmin]) ──

    /// <summary>Platform-wide revenue with per-tenant breakdown. AppMasterAdmin-only.</summary>
    Task<Result<CrossTenantRevenueReport, PolarError>> GetCrossTenantRevenueAsync(CrossTenantRevenueRequest request, CancellationToken ct = default);

    /// <summary>Order volume per tenant ranked descending. AppMasterAdmin-only.</summary>
    Task<Result<CrossTenantOrderVolumeReport, PolarError>> GetCrossTenantOrderVolumeAsync(CrossTenantOrderVolumeRequest request, CancellationToken ct = default);

    /// <summary>Per-tenant webhook event activity from the snapshot Events table. AppMasterAdmin-only.</summary>
    Task<Result<WebhookDeliveryHealthReport, PolarError>> GetWebhookDeliveryHealthAsync(WebhookDeliveryHealthRequest request, CancellationToken ct = default);

    /// <summary>Per-tenant composite health snapshot (recent activity + customer + subscription counts). AppMasterAdmin-only.</summary>
    Task<Result<TenantHealthReport, PolarError>> GetTenantHealthAsync(TenantHealthRequest request, CancellationToken ct = default);
}
