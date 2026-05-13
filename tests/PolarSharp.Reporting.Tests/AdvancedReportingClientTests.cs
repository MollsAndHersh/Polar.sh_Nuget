using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;
using PolarSharp.Reporting.Reports;
using PolarSharp.Reporting.Tests.Infrastructure;
using static PolarSharp.Reporting.Tests.Infrastructure.AdvancedReportsResultExtensions;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Tests for the v1.3.H <see cref="IAdvancedReportingClient"/> — 8 tenant reports + 4
/// cross-tenant operator reports. Verifies aggregation correctness, bucket alignment,
/// tenant scoping (vs cross-tenant IgnoreQueryFilters), and the cohort retention curve.
/// </summary>
public sealed class AdvancedReportingClientTests
{
    private static readonly DateTimeOffset Today = new(2026, 5, 13, 0, 0, 0, TimeSpan.Zero);

    private static void Configure(IServiceCollection s) =>
        s.AddScoped<IAdvancedReportingClient, EfAdvancedReportingClient>();

    [Fact]
    public async Task GetRevenueOverTimeAsync_buckets_monthly_and_computes_totals()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedOrderAsync(ctx, "ord_1", Today.AddDays(-40), amount: 5_000, refunded: 0);
        await SeedOrderAsync(ctx, "ord_2", Today.AddDays(-5),  amount: 10_000, refunded: 1_000);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetRevenueOverTimeAsync(new RevenueOverTimeRequest
        {
            From = Today.AddDays(-60), To = Today.AddDays(1),
            Granularity = ReportBucketGranularity.Monthly,
        })).ValueOrThrow();

        Assert.Equal(2, report.Buckets.Count);                       // two months
        Assert.Equal(15_000, report.TotalGrossRevenue);
        Assert.Equal(14_000, report.TotalNetRevenue);                // 15k - 1k refunded
        Assert.Equal(2, report.TotalOrderCount);
    }

    [Fact]
    public async Task GetTopProductsAsync_ranks_by_revenue_and_by_units()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        var oA = await SeedOrderAsync(ctx, "ord_a", Today, amount: 0, refunded: 0);
        await SeedLineItemAsync(ctx, oA, "prod_widget", "Widget", quantity: 1, lineTotal: 10_000);
        var oB = await SeedOrderAsync(ctx, "ord_b", Today, amount: 0, refunded: 0);
        await SeedLineItemAsync(ctx, oB, "prod_gizmo", "Gizmo", quantity: 5, lineTotal: 5_000);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetTopProductsAsync(new TopProductsRequest { From = Today.AddDays(-1), To = Today.AddDays(1) })).ValueOrThrow();

        Assert.Equal("prod_widget", report.ByRevenue[0].PolarProductId);     // 10k > 5k
        Assert.Equal("prod_gizmo",  report.ByUnits[0].PolarProductId);       // 5 > 1
    }

    [Fact]
    public async Task GetTopCustomersAsync_orders_by_LifetimeValue_descending()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedCustomerAsync(ctx, "cus_1", "alice@x.com", ltv: 5_000);
        await SeedCustomerAsync(ctx, "cus_2", "bob@x.com",   ltv: 50_000);
        await SeedCustomerAsync(ctx, "cus_3", "eve@x.com",   ltv: 20_000);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetTopCustomersAsync(new TopCustomersRequest { Limit = 5 })).ValueOrThrow();

        Assert.Equal(["cus_2", "cus_3", "cus_1"], report.Rows.Select(r => r.PolarCustomerId).ToArray());
    }

    [Fact]
    public async Task GetSubscriptionChurnCohortAsync_tracks_retention_curve_over_months()
    {
        var cohortMonth = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);

        // 3 subscribers, 1 cancels in March, 1 in April, 1 still active
        await SeedSubscriptionAsync(ctx, "sub_1", cohortMonth.AddDays(2), canceledAt: cohortMonth.AddMonths(2).AddDays(5));   // canceled month-2
        await SeedSubscriptionAsync(ctx, "sub_2", cohortMonth.AddDays(3), canceledAt: cohortMonth.AddMonths(3).AddDays(10));  // canceled month-3
        await SeedSubscriptionAsync(ctx, "sub_3", cohortMonth.AddDays(7), canceledAt: null);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetSubscriptionChurnCohortAsync(new SubscriptionChurnCohortRequest
        {
            CohortMonth = cohortMonth,
            MonthsToTrack = 5,
        })).ValueOrThrow();

        Assert.Equal(3, report.CohortSize);
        // Month 0: 3 active. Month 2 (after first cancel): 2 active. Month 3 (after second): 1 active.
        Assert.Equal(3, report.Retention[0].Active);
        Assert.Equal(1, report.Retention[3].Active);
    }

    [Fact]
    public async Task GetRefundRateAsync_computes_rate_per_bucket_and_overall()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedOrderAsync(ctx, "ord_1", Today, amount: 10_000, refunded: 1_000);
        await SeedOrderAsync(ctx, "ord_2", Today, amount: 5_000,  refunded: 0);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetRefundRateAsync(new RefundRateRequest
        {
            From = Today.AddDays(-1), To = Today.AddDays(1),
            Granularity = ReportBucketGranularity.Monthly,
        })).ValueOrThrow();

        // 1000/15000 = 6.67%
        Assert.Equal(6.67m, report.OverallRefundRatePercent);
    }

    [Fact]
    public async Task GetAverageOrderValueAsync_returns_overall_AOV()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedOrderAsync(ctx, "ord_1", Today, amount: 10_000, refunded: 0);
        await SeedOrderAsync(ctx, "ord_2", Today, amount: 20_000, refunded: 0);
        await SeedOrderAsync(ctx, "ord_3", Today, amount: 30_000, refunded: 0);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetAverageOrderValueAsync(new AverageOrderValueRequest
        {
            From = Today.AddDays(-1), To = Today.AddDays(1),
            Granularity = ReportBucketGranularity.Daily,
        })).ValueOrThrow();

        Assert.Equal(20_000m, report.OverallAverage);
    }

    [Fact]
    public async Task GetCustomerLifetimeValueDistributionAsync_buckets_customers_correctly()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedCustomerAsync(ctx, "c1", "1@x.com", ltv: 1_000);     // bucket [0, 5_000)
        await SeedCustomerAsync(ctx, "c2", "2@x.com", ltv: 6_000);     // bucket [5_000, 25_000)
        await SeedCustomerAsync(ctx, "c3", "3@x.com", ltv: 30_000);    // bucket [25_000, ∞)
        await SeedCustomerAsync(ctx, "c4", "4@x.com", ltv: 100_000);   // bucket [25_000, ∞)

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetCustomerLifetimeValueDistributionAsync(new CustomerLifetimeValueDistributionRequest
        {
            BucketUpperBoundsExclusive = [5_000, 25_000],
        })).ValueOrThrow();

        Assert.Equal(3, report.Buckets.Count);
        Assert.Equal(1, report.Buckets[0].CustomerCount);    // [0, 5k)
        Assert.Equal(1, report.Buckets[1].CustomerCount);    // [5k, 25k)
        Assert.Equal(2, report.Buckets[2].CustomerCount);    // [25k, ∞)
    }

    [Fact]
    public async Task GetCurrencyMixAsync_groups_orders_by_currency_with_share_percent()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedOrderAsync(ctx, "u1", Today, amount: 8_000, currency: "USD");
        await SeedOrderAsync(ctx, "u2", Today, amount: 2_000, currency: "USD");
        await SeedOrderAsync(ctx, "e1", Today, amount: 5_000, currency: "EUR");

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetCurrencyMixAsync(new CurrencyMixRequest
        {
            From = Today.AddDays(-1), To = Today.AddDays(1),
        })).ValueOrThrow();

        Assert.Equal(2, report.Rows.Count);
        Assert.Equal("USD", report.Rows[0].Currency);                // ranked by revenue
        Assert.Equal(10_000, report.Rows[0].GrossRevenue);
        Assert.Equal(66.67m, report.Rows[0].SharePercent);            // 10k/15k
    }

    [Fact]
    public async Task GetCrossTenantRevenueAsync_aggregates_across_all_tenants_via_IgnoreQueryFilters()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedOrderAsync(ctx, "ord_a", Today, amount: 100_000, tenantId: "tenant-A");
        await SeedOrderAsync(ctx, "ord_b", Today, amount: 50_000,  tenantId: "tenant-B");
        // Snapshot DbContext defaults to the harness tenant "tenant-test" — the cross-tenant
        // report bypasses that filter via IgnoreQueryFilters.

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetCrossTenantRevenueAsync(new CrossTenantRevenueRequest
        {
            From = Today.AddDays(-1), To = Today.AddDays(1),
        })).ValueOrThrow();

        Assert.Equal(150_000, report.PlatformGrossRevenue);
        Assert.Equal(2, report.ActiveTenantCount);
        Assert.Equal("tenant-A", report.ByTenant[0].TenantId);        // ranked by revenue
    }

    [Fact]
    public async Task GetCrossTenantOrderVolumeAsync_ranks_tenants_by_order_count()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedOrderAsync(ctx, "a1", Today, tenantId: "tenant-A", customerId: "cus_1");
        await SeedOrderAsync(ctx, "a2", Today, tenantId: "tenant-A", customerId: "cus_2");
        await SeedOrderAsync(ctx, "b1", Today, tenantId: "tenant-B", customerId: "cus_3");

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetCrossTenantOrderVolumeAsync(new CrossTenantOrderVolumeRequest
        {
            From = Today.AddDays(-1), To = Today.AddDays(1),
        })).ValueOrThrow();

        Assert.Equal(2, report.Rows.Count);
        Assert.Equal("tenant-A", report.Rows[0].TenantId);
        Assert.Equal(2, report.Rows[0].OrderCount);
        Assert.Equal(2, report.Rows[0].CustomerCount);                // distinct customer ids
    }

    [Fact]
    public async Task GetWebhookDeliveryHealthAsync_categorises_events_by_type_prefix()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedEventAsync(ctx, "evt_1", "order.created",        Today, tenantId: "tenant-A");
        await SeedEventAsync(ctx, "evt_2", "order.paid",           Today, tenantId: "tenant-A");
        await SeedEventAsync(ctx, "evt_3", "subscription.active",  Today, tenantId: "tenant-A");
        await SeedEventAsync(ctx, "evt_4", "refund.created",       Today, tenantId: "tenant-B");

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetWebhookDeliveryHealthAsync(new WebhookDeliveryHealthRequest
        {
            From = Today.AddDays(-1), To = Today.AddDays(1),
        })).ValueOrThrow();

        var a = report.Rows.Single(r => r.TenantId == "tenant-A");
        Assert.Equal(3, a.TotalEvents);
        Assert.Equal(2, a.OrderCreatedEvents);
        Assert.Equal(1, a.SubscriptionEvents);

        var b = report.Rows.Single(r => r.TenantId == "tenant-B");
        Assert.Equal(1, b.RefundEvents);
    }

    [Fact]
    public async Task GetTenantHealthAsync_grades_tenants_based_on_recent_activity_and_customer_presence()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        // Healthy: customers + recent orders
        await SeedCustomerAsync(ctx, "c-h", "h@x.com", ltv: 10_000, tenantId: "tenant-Healthy");
        await SeedOrderAsync(ctx, "ord-h", Today.AddDays(-2), amount: 1_000, tenantId: "tenant-Healthy");

        // Dormant: customers but no recent orders
        await SeedCustomerAsync(ctx, "c-d", "d@x.com", ltv: 5_000, tenantId: "tenant-Dormant");
        await SeedOrderAsync(ctx, "ord-d", Today.AddDays(-100), amount: 5_000, tenantId: "tenant-Dormant");

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetTenantHealthAsync(new TenantHealthRequest
        {
            RecentActivityCutoff = Today.AddDays(-30),
        })).ValueOrThrow();

        Assert.Equal(TenantHealthGrade.Healthy, report.Rows.Single(r => r.TenantId == "tenant-Healthy").Grade);
        Assert.Equal(TenantHealthGrade.Dormant, report.Rows.Single(r => r.TenantId == "tenant-Dormant").Grade);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedOrderAsync(
        ReportingTestContext ctx,
        string polarOrderId,
        DateTimeOffset createdAt,
        long amount = 0,
        long refunded = 0,
        string currency = "USD",
        string? tenantId = null,
        string customerId = "cus_default")
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var id = Guid.NewGuid();
        db.Orders.Add(new ReportOrderEntity
        {
            Id = id,
            TenantId = tenantId ?? ctx.CurrentTenantId,
            PolarOrderId = polarOrderId,
            OrderNumber = polarOrderId.ToUpper(),
            CustomerId = customerId,
            Status = "paid",
            Amount = amount,
            RefundedAmount = refunded,
            Currency = currency,
            CreatedAt = createdAt,
        });
        if (refunded > 0)
        {
            db.OrderRefunds.Add(new ReportOrderRefundEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId ?? ctx.CurrentTenantId,
                OrderId = id,
                PolarRefundId = $"ref_{polarOrderId}",
                Amount = refunded,
                Currency = currency,
                Reason = "customer_request",
                CreatedAt = createdAt,
            });
        }
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task SeedLineItemAsync(ReportingTestContext ctx, Guid orderId, string productId, string productName, int quantity, long lineTotal)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        db.OrderLineItems.Add(new ReportOrderLineItemEntity
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.CurrentTenantId,
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            LineTotal = lineTotal,
            UnitAmount = quantity == 0 ? 0 : lineTotal / quantity,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCustomerAsync(ReportingTestContext ctx, string polarCustomerId, string email, long ltv, string? tenantId = null)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        db.Customers.Add(new ReportCustomerEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? ctx.CurrentTenantId,
            PolarCustomerId = polarCustomerId,
            Email = email,
            Currency = "USD",
            LifetimeValue = ltv,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedSubscriptionAsync(ReportingTestContext ctx, string polarId, DateTimeOffset startedAt, DateTimeOffset? canceledAt)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        db.Subscriptions.Add(new ReportSubscriptionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.CurrentTenantId,
            PolarSubscriptionId = polarId,
            CustomerId = "cus_default",
            ProductId = "prod_default",
            Status = canceledAt is null ? "active" : "canceled",
            StartedAt = startedAt,
            CanceledAt = canceledAt,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedEventAsync(ReportingTestContext ctx, string polarEventId, string type, DateTimeOffset occurredAt, string? tenantId = null)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        db.Events.Add(new ReportEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? ctx.CurrentTenantId,
            PolarEventId = polarEventId,
            Type = type,
            OccurredAt = occurredAt,
        });
        await db.SaveChangesAsync();
    }
}
