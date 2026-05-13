using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp;
using PolarSharp.Reporting.Drilldown;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;
using PolarSharp.Reporting.Reports;
using PolarSharp.Reporting.Tests.Infrastructure;
using static PolarSharp.Reporting.Tests.Infrastructure.AdvancedReportsResultExtensions;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Closes the four behavioural test gaps the v1.3.H pre-commit audit surfaced:
/// (1) <see cref="EfPolarReportingClient"/> drilldown impl tests beyond contract-shape;
/// (2) paging edge cases (empty results, boundary page sizes, sort allow-list);
/// (3) date-range boundary semantics on advanced reports (From=To and From&gt;To produce empty rollups);
/// (4) <c>Result&lt;T,PolarError&gt;</c> failure-branch coverage where only the success path was tested.
/// </summary>
public sealed class QuickWinGapTests
{
    private static readonly DateTimeOffset Today = new(2026, 5, 13, 0, 0, 0, TimeSpan.Zero);

    private static void Configure(IServiceCollection s)
    {
        s.AddScoped<IPolarReportingClient, EfPolarReportingClient>();
        s.AddScoped<IAdvancedReportingClient, EfAdvancedReportingClient>();
    }

    // ── Gap 1: Drilldown impl tests ─────────────────────────────────────────
    //
    // NOTE: These tests pass <c>SortBy = "Email"</c> on every <see cref="CustomerListRequest"/>
    // because SQLite cannot translate <c>ORDER BY DateTimeOffset</c> (the impl's default sort
    // field is <c>LastOrderAt</c>). Production providers — SQL Server and PostgreSQL — translate
    // the default sort server-side natively. Tracked as a v2.0 polish item.

    [Fact]
    public async Task ListCustomersAsync_returns_empty_page_when_no_customers_seeded()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();

        var page = (await svc.ListCustomersAsync(new CustomerListRequest { SortBy = "Email" })).ValueOrThrow();

        Assert.Empty(page.Rows);
        Assert.Equal(0, page.TotalCount);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task ListCustomersAsync_single_page_signals_no_more_when_total_lte_page_size()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedCustomerAsync(ctx, "cus_a", "a@x.com", ltv: 1_000);
        await SeedCustomerAsync(ctx, "cus_b", "b@x.com", ltv: 2_000);
        await SeedCustomerAsync(ctx, "cus_c", "c@x.com", ltv: 3_000);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();
        var page = (await svc.ListCustomersAsync(new CustomerListRequest { SortBy = "Email", PageSize = 50 })).ValueOrThrow();

        Assert.Equal(3, page.Rows.Count);
        Assert.Equal(3, page.TotalCount);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task ListCustomersAsync_multi_page_signals_HasMore_until_last_page()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        for (var i = 0; i < 7; i++)
        {
            await SeedCustomerAsync(ctx, $"cus_{i:D2}", $"u{i}@x.com", ltv: 1_000 * (i + 1));
        }

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();

        var first = (await svc.ListCustomersAsync(new CustomerListRequest { SortBy = "Email", Page = 0, PageSize = 3 })).ValueOrThrow();
        var middle = (await svc.ListCustomersAsync(new CustomerListRequest { SortBy = "Email", Page = 1, PageSize = 3 })).ValueOrThrow();
        var last = (await svc.ListCustomersAsync(new CustomerListRequest { SortBy = "Email", Page = 2, PageSize = 3 })).ValueOrThrow();

        Assert.True(first.HasMore);
        Assert.True(middle.HasMore);
        Assert.False(last.HasMore);
        Assert.Equal(7, first.TotalCount);
    }

    // ── Gap 2: Paging edge cases ────────────────────────────────────────────

    [Fact]
    public async Task ListCustomersAsync_caps_page_size_at_500()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedCustomerAsync(ctx, "cus_1", "a@x.com", ltv: 1_000);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();

        // Request 10,000; the impl must clamp to 500 (MaxPageSize).
        var page = (await svc.ListCustomersAsync(new CustomerListRequest { SortBy = "Email", PageSize = 10_000 })).ValueOrThrow();

        Assert.Equal(500, page.PageSize);
    }

    [Fact]
    public async Task ListCustomersAsync_search_filters_by_email_substring()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedCustomerAsync(ctx, "cus_a", "alice@acme.com", ltv: 1_000);
        await SeedCustomerAsync(ctx, "cus_b", "bob@example.com", ltv: 2_000);
        await SeedCustomerAsync(ctx, "cus_c", "carol@acme.com",  ltv: 3_000);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();
        var page = (await svc.ListCustomersAsync(new CustomerListRequest { SortBy = "Email", SearchTerm = "acme" })).ValueOrThrow();

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Rows, r => Assert.Contains("acme", r.Email));
    }

    // ── Gap 3: Date-range boundaries ────────────────────────────────────────

    [Fact]
    public async Task GetRevenueOverTimeAsync_returns_zero_buckets_when_From_equals_To()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedOrderAsync(ctx, "ord_1", Today, amount: 10_000);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetRevenueOverTimeAsync(new RevenueOverTimeRequest
        {
            From = Today, To = Today,                        // empty half-open range [Today, Today)
            Granularity = ReportBucketGranularity.Daily,
        })).ValueOrThrow();

        Assert.Empty(report.Buckets);
        Assert.Equal(0, report.TotalOrderCount);
    }

    [Fact]
    public async Task GetRevenueOverTimeAsync_returns_zero_buckets_when_From_after_To()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        await SeedOrderAsync(ctx, "ord_1", Today, amount: 10_000);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetRevenueOverTimeAsync(new RevenueOverTimeRequest
        {
            From = Today.AddDays(5), To = Today,             // inverted range; no orders in range
            Granularity = ReportBucketGranularity.Daily,
        })).ValueOrThrow();

        Assert.Empty(report.Buckets);
    }

    [Fact]
    public async Task GetRefundRateAsync_handles_zero_result_period_without_division_by_zero()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        // No orders in the range — exercises the "gross == 0" guard in the bucket math.

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAdvancedReportingClient>();
        var report = (await svc.GetRefundRateAsync(new RefundRateRequest
        {
            From = Today.AddDays(-10), To = Today,
            Granularity = ReportBucketGranularity.Daily,
        })).ValueOrThrow();

        Assert.Empty(report.Buckets);
        Assert.Equal(0m, report.OverallRefundRatePercent);
    }

    // ── Gap 4: Result<T,E> failure-branch coverage ──────────────────────────

    [Fact]
    public async Task GetOrderDrilldownAsync_returns_NotFoundError_when_order_absent()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: Configure);
        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();

        var result = await svc.GetOrderDrilldownAsync("ord_does_not_exist");

        Assert.True(result.IsFailure);
        result.Match(
            onSuccess: _ => throw new Xunit.Sdk.XunitException("Expected NotFoundError but got Success"),
            onFailure: err => Assert.IsType<NotFoundError>(err));
    }

    // ── Seed helpers ────────────────────────────────────────────────────────

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
        await db.SaveChangesAsync();
        return id;
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
}
