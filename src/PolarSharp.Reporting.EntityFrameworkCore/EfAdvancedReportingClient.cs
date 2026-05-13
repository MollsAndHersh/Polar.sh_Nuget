using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PolarSharp.Reporting.Reports;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;

namespace PolarSharp.Reporting.EntityFrameworkCore;

/// <summary>
/// EF-backed <see cref="IAdvancedReportingClient"/> implementation. Reads from the snapshot
/// tables populated by <c>IReportSnapshotService</c>. Tenant reports use
/// the global query filter for scoping; cross-tenant operator reports use
/// <c>IgnoreQueryFilters</c> on their reads and rely on <c>[RequireAppMasterAdmin]</c> at
/// the endpoint layer.
/// </summary>
/// <remarks>
/// Aggregations that SQLite can't translate (Min/Max on <c>DateTimeOffset</c>, decimal
/// arithmetic with rounding precision) materialise the relevant slice and aggregate
/// client-side. Tenant data volumes per snapshot run are bounded, so this is fine.
/// </remarks>
public sealed class EfAdvancedReportingClient(
    PolarReportingDbContext db,
    ILogger<EfAdvancedReportingClient> logger) : IAdvancedReportingClient
{
    private readonly PolarReportingDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly ILogger<EfAdvancedReportingClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // ── 1. Revenue over time ───────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<RevenueOverTimeReport, PolarError>> GetRevenueOverTimeAsync(RevenueOverTimeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // SQLite can't translate DateTimeOffset range filters when combined with the global
        // tenant filter expression. Materialise post-tenant-filter, then narrow in memory.
        // Production providers (SQL Server / PostgreSQL) handle this server-side natively.
        var orders = (await _db.Orders.AsNoTracking()
            .Select(o => new { o.CreatedAt, o.Amount, o.RefundedAmount })
            .ToListAsync(ct).ConfigureAwait(false))
            .Where(o => o.CreatedAt >= request.From && o.CreatedAt < request.To)
            .ToList();

        var bucketed = orders
            .GroupBy(o => BucketKey(o.CreatedAt, request.Granularity))
            .OrderBy(g => g.Key)
            .Select(g => new RevenueBucket(
                BucketStart: g.Key,
                GrossRevenue: g.Sum(o => o.Amount),
                RefundedAmount: g.Sum(o => o.RefundedAmount),
                NetRevenue: g.Sum(o => o.Amount - o.RefundedAmount),
                OrderCount: g.Count()))
            .ToList();

        return Result<RevenueOverTimeReport, PolarError>.Success(new RevenueOverTimeReport
        {
            Buckets = bucketed,
            TotalGrossRevenue = bucketed.Sum(b => b.GrossRevenue),
            TotalNetRevenue = bucketed.Sum(b => b.NetRevenue),
            TotalOrderCount = bucketed.Sum(b => b.OrderCount),
        });
    }

    // ── 2. Top products ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<TopProductsReport, PolarError>> GetTopProductsAsync(TopProductsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var limit = Math.Clamp(request.Limit, 1, 100);

        var inRangeOrderIds = (await _db.Orders.AsNoTracking()
            .Select(o => new { o.Id, o.CreatedAt })
            .ToListAsync(ct).ConfigureAwait(false))
            .Where(o => o.CreatedAt >= request.From && o.CreatedAt < request.To)
            .Select(o => o.Id)
            .ToList();

        if (inRangeOrderIds.Count == 0)
        {
            return Result<TopProductsReport, PolarError>.Success(new TopProductsReport
            {
                ByRevenue = [],
                ByUnits = [],
            });
        }

        var items = await _db.OrderLineItems.AsNoTracking()
            .Where(li => inRangeOrderIds.Contains(li.OrderId))
            .Select(li => new { li.OrderId, li.ProductId, li.ProductName, li.Quantity, li.LineTotal })
            .ToListAsync(ct).ConfigureAwait(false);

        var grouped = items.GroupBy(i => new { i.ProductId, i.ProductName }).Select(g => new TopProductRow(
            PolarProductId: g.Key.ProductId,
            ProductName: g.Key.ProductName,
            Revenue: g.Sum(i => i.LineTotal),
            UnitsSold: g.Sum(i => i.Quantity),
            OrderCount: g.Select(i => i.OrderId).Distinct().Count())).ToList();

        return Result<TopProductsReport, PolarError>.Success(new TopProductsReport
        {
            ByRevenue = grouped.OrderByDescending(r => r.Revenue).Take(limit).ToList(),
            ByUnits = grouped.OrderByDescending(r => r.UnitsSold).Take(limit).ToList(),
        });
    }

    // ── 3. Top customers ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<TopCustomersReport, PolarError>> GetTopCustomersAsync(TopCustomersRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var limit = Math.Clamp(request.Limit, 1, 100);

        // OrderByDescending(LifetimeValue) needs to materialise to also project FirstOrderAt/LastOrderAt
        // which SQLite can't Min/Max server-side. Fine — the customers table is bounded per tenant.
        var topRows = await _db.Customers.AsNoTracking()
            .OrderByDescending(c => c.LifetimeValue)
            .Take(limit)
            .Select(c => new
            {
                c.PolarCustomerId, c.Email, c.Name, c.LifetimeValue, c.Currency, c.OrderCount, c.FirstOrderAt, c.LastOrderAt,
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var rows = topRows.Select(c => new TopCustomerRow(
            c.PolarCustomerId, c.Email, c.Name, c.LifetimeValue, c.Currency, c.OrderCount, c.FirstOrderAt, c.LastOrderAt)).ToList();

        return Result<TopCustomersReport, PolarError>.Success(new TopCustomersReport(rows));
    }

    // ── 4. Subscription churn cohort ───────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<SubscriptionChurnCohortReport, PolarError>> GetSubscriptionChurnCohortAsync(SubscriptionChurnCohortRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cohortStart = new DateTimeOffset(request.CohortMonth.Year, request.CohortMonth.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var cohortEnd = cohortStart.AddMonths(1);
        var monthsToTrack = Math.Clamp(request.MonthsToTrack, 1, 36);

        var cohort = (await _db.Subscriptions.AsNoTracking()
            .Select(s => new { s.StartedAt, s.CanceledAt })
            .ToListAsync(ct).ConfigureAwait(false))
            .Where(s => s.StartedAt >= cohortStart && s.StartedAt < cohortEnd)
            .ToList();

        var size = cohort.Count;
        var retention = new List<ChurnRetentionPoint>(monthsToTrack + 1);
        for (var month = 0; month <= monthsToTrack; month++)
        {
            var asOf = cohortStart.AddMonths(month + 1);     // count "still active at end of month N"
            var canceled = cohort.Count(s => s.CanceledAt is { } c && c < asOf);
            var active = size - canceled;
            var percent = size == 0 ? 0m : Math.Round((decimal)active / size * 100m, 2);
            retention.Add(new ChurnRetentionPoint(month, active, canceled, percent));
        }

        return Result<SubscriptionChurnCohortReport, PolarError>.Success(new SubscriptionChurnCohortReport
        {
            CohortMonth = cohortStart,
            CohortSize = size,
            Retention = retention,
        });
    }

    // ── 5. Refund rate ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<RefundRateReport, PolarError>> GetRefundRateAsync(RefundRateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orders = (await _db.Orders.AsNoTracking()
            .Select(o => new { o.Id, o.CreatedAt, o.Amount, o.RefundedAmount })
            .ToListAsync(ct).ConfigureAwait(false))
            .Where(o => o.CreatedAt >= request.From && o.CreatedAt < request.To)
            .ToList();

        var orderIds = orders.Select(o => o.Id).ToList();
        var refunds = await _db.OrderRefunds.AsNoTracking()
            .Where(r => orderIds.Contains(r.OrderId))
            .Select(r => new { r.OrderId, r.CreatedAt, r.Amount })
            .ToListAsync(ct).ConfigureAwait(false);

        var refundsByOrder = refunds.GroupBy(r => r.OrderId).ToDictionary(g => g.Key, g => g.ToList());

        var buckets = orders
            .GroupBy(o => BucketKey(o.CreatedAt, request.Granularity))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var gross = g.Sum(o => o.Amount);
                var refundCount = g.SelectMany(o => refundsByOrder.GetValueOrDefault(o.Id) ?? []).Count();
                var refundedAmt = g.Sum(o => o.RefundedAmount);
                var rate = gross == 0 ? 0m : Math.Round((decimal)refundedAmt / gross * 100m, 2);
                return new RefundRateBucket(g.Key, gross, refundedAmt, g.Count(), refundCount, rate);
            })
            .ToList();

        var totalGross = buckets.Sum(b => b.GrossRevenue);
        var totalRefunded = buckets.Sum(b => b.RefundedAmount);
        var overall = totalGross == 0 ? 0m : Math.Round((decimal)totalRefunded / totalGross * 100m, 2);
        return Result<RefundRateReport, PolarError>.Success(new RefundRateReport(buckets, overall));
    }

    // ── 6. Average order value ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<AverageOrderValueReport, PolarError>> GetAverageOrderValueAsync(AverageOrderValueRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orders = (await _db.Orders.AsNoTracking()
            .Select(o => new { o.CreatedAt, o.Amount })
            .ToListAsync(ct).ConfigureAwait(false))
            .Where(o => o.CreatedAt >= request.From && o.CreatedAt < request.To)
            .ToList();

        var buckets = orders
            .GroupBy(o => BucketKey(o.CreatedAt, request.Granularity))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var count = g.Count();
                var avg = count == 0 ? 0m : Math.Round((decimal)g.Sum(o => o.Amount) / count, 2);
                return new AverageOrderValueBucket(g.Key, count, avg);
            })
            .ToList();

        var overall = orders.Count == 0 ? 0m : Math.Round((decimal)orders.Sum(o => o.Amount) / orders.Count, 2);
        return Result<AverageOrderValueReport, PolarError>.Success(new AverageOrderValueReport(buckets, overall));
    }

    // ── 7. Customer LTV distribution ───────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<CustomerLifetimeValueDistributionReport, PolarError>> GetCustomerLifetimeValueDistributionAsync(CustomerLifetimeValueDistributionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.BucketUpperBoundsExclusive.Count == 0)
        {
            return Result<CustomerLifetimeValueDistributionReport, PolarError>.Success(
                new CustomerLifetimeValueDistributionReport([]));
        }

        var values = await _db.Customers.AsNoTracking()
            .Select(c => c.LifetimeValue)
            .ToListAsync(ct).ConfigureAwait(false);

        var bounds = request.BucketUpperBoundsExclusive.OrderBy(b => b).ToList();
        var buckets = new List<LtvBucket>(bounds.Count + 1);
        long? lower = null;
        foreach (var upper in bounds)
        {
            var inBucket = values.Where(v => v >= (lower ?? 0) && v < upper).ToList();
            buckets.Add(new LtvBucket(lower, upper, inBucket.Count, inBucket.Sum()));
            lower = upper;
        }
        var tail = values.Where(v => v >= (lower ?? 0)).ToList();
        buckets.Add(new LtvBucket(lower, null, tail.Count, tail.Sum()));

        return Result<CustomerLifetimeValueDistributionReport, PolarError>.Success(
            new CustomerLifetimeValueDistributionReport(buckets));
    }

    // ── 8. Currency mix ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<CurrencyMixReport, PolarError>> GetCurrencyMixAsync(CurrencyMixRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orders = (await _db.Orders.AsNoTracking()
            .Select(o => new { o.CreatedAt, o.Currency, o.Amount, o.RefundedAmount })
            .ToListAsync(ct).ConfigureAwait(false))
            .Where(o => o.CreatedAt >= request.From && o.CreatedAt < request.To)
            .ToList();

        var totalGross = orders.Sum(o => (long)o.Amount);
        var rows = orders
            .GroupBy(o => o.Currency)
            .Select(g =>
            {
                var gross = g.Sum(o => o.Amount);
                var net = g.Sum(o => o.Amount - o.RefundedAmount);
                var share = totalGross == 0 ? 0m : Math.Round((decimal)gross / totalGross * 100m, 2);
                return new CurrencyMixRow(g.Key, gross, net, g.Count(), share);
            })
            .OrderByDescending(r => r.GrossRevenue)
            .ToList();

        return Result<CurrencyMixReport, PolarError>.Success(new CurrencyMixReport(rows));
    }

    // ── 9. Cross-tenant revenue ────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<CrossTenantRevenueReport, PolarError>> GetCrossTenantRevenueAsync(CrossTenantRevenueRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var top = Math.Clamp(request.TopTenants, 1, 500);

        var orders = (await _db.Orders.IgnoreQueryFilters().AsNoTracking()
            .Select(o => new { o.TenantId, o.CreatedAt, o.Amount, o.RefundedAmount })
            .ToListAsync(ct).ConfigureAwait(false))
            .Where(o => o.CreatedAt >= request.From && o.CreatedAt < request.To)
            .ToList();

        var perTenant = orders
            .GroupBy(o => o.TenantId)
            .Select(g => new TenantRevenueRow(
                TenantId: g.Key,
                GrossRevenue: g.Sum(o => o.Amount),
                NetRevenue: g.Sum(o => o.Amount - o.RefundedAmount),
                OrderCount: g.Count()))
            .OrderByDescending(r => r.GrossRevenue)
            .ToList();

        var topRows = perTenant.Take(top).ToList();
        if (perTenant.Count > top)
        {
            var rest = perTenant.Skip(top).ToList();
            topRows.Add(new TenantRevenueRow(
                TenantId: "(other)",
                GrossRevenue: rest.Sum(r => r.GrossRevenue),
                NetRevenue: rest.Sum(r => r.NetRevenue),
                OrderCount: rest.Sum(r => r.OrderCount)));
        }

        return Result<CrossTenantRevenueReport, PolarError>.Success(new CrossTenantRevenueReport
        {
            PlatformGrossRevenue = perTenant.Sum(r => r.GrossRevenue),
            PlatformNetRevenue = perTenant.Sum(r => r.NetRevenue),
            ActiveTenantCount = perTenant.Count,
            ByTenant = topRows,
        });
    }

    // ── 10. Cross-tenant order volume ──────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<CrossTenantOrderVolumeReport, PolarError>> GetCrossTenantOrderVolumeAsync(CrossTenantOrderVolumeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var limit = Math.Clamp(request.Limit, 1, 500);

        var orders = (await _db.Orders.IgnoreQueryFilters().AsNoTracking()
            .Select(o => new { o.TenantId, o.CustomerId, o.CreatedAt })
            .ToListAsync(ct).ConfigureAwait(false))
            .Where(o => o.CreatedAt >= request.From && o.CreatedAt < request.To)
            .ToList();

        var rows = orders
            .GroupBy(o => o.TenantId)
            .Select(g => new TenantOrderVolumeRow(
                TenantId: g.Key,
                OrderCount: g.Count(),
                CustomerCount: g.Select(o => o.CustomerId).Distinct().Count(),
                LastOrderAt: g.Max(o => o.CreatedAt)))
            .OrderByDescending(r => r.OrderCount)
            .Take(limit)
            .ToList();

        return Result<CrossTenantOrderVolumeReport, PolarError>.Success(new CrossTenantOrderVolumeReport(rows));
    }

    // ── 11. Webhook delivery health ────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<WebhookDeliveryHealthReport, PolarError>> GetWebhookDeliveryHealthAsync(WebhookDeliveryHealthRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var events = (await _db.Events.IgnoreQueryFilters().AsNoTracking()
            .Select(e => new { e.TenantId, e.Type, e.OccurredAt })
            .ToListAsync(ct).ConfigureAwait(false))
            .Where(e => e.OccurredAt >= request.From && e.OccurredAt < request.To)
            .ToList();

        var rows = events
            .GroupBy(e => e.TenantId)
            .Select(g => new WebhookDeliveryHealthRow(
                TenantId: g.Key,
                TotalEvents: g.Count(),
                OrderCreatedEvents: g.Count(e => e.Type.StartsWith("order.", StringComparison.Ordinal)),
                SubscriptionEvents: g.Count(e => e.Type.StartsWith("subscription.", StringComparison.Ordinal)),
                RefundEvents: g.Count(e => e.Type.StartsWith("refund.", StringComparison.Ordinal)),
                LastEventAt: g.Max(e => e.OccurredAt)))
            .OrderByDescending(r => r.TotalEvents)
            .ToList();

        return Result<WebhookDeliveryHealthReport, PolarError>.Success(new WebhookDeliveryHealthReport(rows));
    }

    // ── 12. Tenant health ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<TenantHealthReport, PolarError>> GetTenantHealthAsync(TenantHealthRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var customers = await _db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Select(c => new { c.TenantId, c.PolarCustomerId })
            .ToListAsync(ct).ConfigureAwait(false);
        var customersByTenant = customers.GroupBy(c => c.TenantId)
            .ToDictionary(g => g.Key, g => g.Count());

        var orders = await _db.Orders.IgnoreQueryFilters().AsNoTracking()
            .Select(o => new { o.TenantId, o.CreatedAt })
            .ToListAsync(ct).ConfigureAwait(false);
        var ordersByTenant = orders.GroupBy(o => o.TenantId)
            .ToDictionary(g => g.Key, g => new
            {
                Recent = g.Count(o => o.CreatedAt >= request.RecentActivityCutoff),
                LastAt = g.Max(o => o.CreatedAt),
            });

        var subs = await _db.Subscriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.CanceledAt == null)
            .Select(s => s.TenantId)
            .ToListAsync(ct).ConfigureAwait(false);
        var activeSubsByTenant = subs.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

        var allTenantIds = customersByTenant.Keys
            .Union(ordersByTenant.Keys)
            .Union(activeSubsByTenant.Keys)
            .OrderBy(t => t)
            .ToList();

        var rows = allTenantIds.Select(t =>
        {
            var custs = customersByTenant.GetValueOrDefault(t, 0);
            var ordSummary = ordersByTenant.GetValueOrDefault(t);
            var recent = ordSummary?.Recent ?? 0;
            var lastAt = ordSummary?.LastAt;
            var activeSubs = activeSubsByTenant.GetValueOrDefault(t, 0);

            var grade = (custs, recent) switch
            {
                (0, 0) => TenantHealthGrade.Empty,
                (> 0, 0) => TenantHealthGrade.Dormant,
                _ => TenantHealthGrade.Healthy,
            };
            return new TenantHealthRow(t, grade, recent, custs, activeSubs, lastAt);
        }).ToList();

        return Result<TenantHealthReport, PolarError>.Success(new TenantHealthReport(rows));
    }

    // ── Time bucket key helper ─────────────────────────────────────────────

    private static DateTimeOffset BucketKey(DateTimeOffset when, ReportBucketGranularity g) => g switch
    {
        ReportBucketGranularity.Daily => new DateTimeOffset(when.Year, when.Month, when.Day, 0, 0, 0, TimeSpan.Zero),
        ReportBucketGranularity.Weekly => StartOfWeek(when),
        ReportBucketGranularity.Monthly => new DateTimeOffset(when.Year, when.Month, 1, 0, 0, 0, TimeSpan.Zero),
        _ => throw new ArgumentOutOfRangeException(nameof(g), g, "Unknown bucket granularity."),
    };

    /// <summary>ISO-8601 week start (Monday at 00:00:00 UTC).</summary>
    private static DateTimeOffset StartOfWeek(DateTimeOffset when)
    {
        var date = when.UtcDateTime.Date;
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        var monday = date.AddDays(-diff);
        return new DateTimeOffset(monday, TimeSpan.Zero);
    }
}
