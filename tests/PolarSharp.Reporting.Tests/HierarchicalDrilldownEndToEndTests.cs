using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.Reporting.Drilldown;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;
using PolarSharp.Reporting.Tests.Infrastructure;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// V20-005 acceptance: drilldown grid backed by snapshot DB. Functional coverage
/// (paging, sorting, filtering, scope-by-customer, cross-tenant isolation) plus the
/// performance assertion (top-level customer page returns &lt;100ms for a 10k-customer
/// tenant). The perf assertion is gated by <c>[Trait("Category", "Performance")]</c>
/// so it skips during the regular unit test gate; CI's <c>publish</c> step (or a
/// developer running locally) opt in.
/// </summary>
public sealed class HierarchicalDrilldownEndToEndTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    [Fact]
    public async Task ListCustomersAsync_returns_paged_rows_in_default_LastOrderAt_desc_order()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(initialTenantId: TenantA,
            configureServices: s => s.AddScoped<IPolarReportingClient, EfPolarReportingClient>());

        await SeedCustomersAsync(ctx, TenantA, count: 12, baseLastOrderAt: DateTimeOffset.UtcNow);

        using var scope = ctx.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();

        var page1 = await client.ListCustomersAsync(new CustomerListRequest { Page = 0, PageSize = 5 });
        Assert.True(page1.IsSuccess);
        page1.Match<int>(
            onSuccess: p =>
            {
                Assert.Equal(12, p.TotalCount);
                Assert.Equal(5, p.Rows.Count);
                Assert.True(p.HasMore);
                // Default sort is LastOrderAt DESC — row[0] should be the most recent.
                Assert.True(p.Rows[0].LastOrderAt >= p.Rows[^1].LastOrderAt);
                return 0;
            },
            onFailure: e => throw new Xunit.Sdk.XunitException(e.Message));
    }

    [Fact]
    public async Task ListCustomersAsync_searchTerm_filters_by_email()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(initialTenantId: TenantA,
            configureServices: s => s.AddScoped<IPolarReportingClient, EfPolarReportingClient>());

        using (var s = ctx.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Customers.Add(NewCustomer(TenantA, "alice@acme.com"));
            db.Customers.Add(NewCustomer(TenantA, "bob@example.com"));
            db.Customers.Add(NewCustomer(TenantA, "carol@acme.com"));
            await db.SaveChangesAsync();
        }

        using var scope = ctx.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();
        var page = await client.ListCustomersAsync(new CustomerListRequest { SearchTerm = "acme" });

        page.Match<int>(
            onSuccess: p =>
            {
                Assert.Equal(2, p.TotalCount);
                Assert.All(p.Rows, r => Assert.Contains("acme", r.Email, StringComparison.OrdinalIgnoreCase));
                return 0;
            },
            onFailure: e => throw new Xunit.Sdk.XunitException(e.Message));
    }

    [Fact]
    public async Task ListOrdersForCustomerAsync_only_returns_orders_for_that_customer()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(initialTenantId: TenantA,
            configureServices: s => s.AddScoped<IPolarReportingClient, EfPolarReportingClient>());

        using (var s = ctx.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Customers.Add(NewCustomer(TenantA, "cust-1@x.com", customerId: "cust-1"));
            db.Customers.Add(NewCustomer(TenantA, "cust-2@x.com", customerId: "cust-2"));
            db.Orders.Add(NewOrder(TenantA, customerId: "cust-1", orderId: "ord-1a"));
            db.Orders.Add(NewOrder(TenantA, customerId: "cust-1", orderId: "ord-1b"));
            db.Orders.Add(NewOrder(TenantA, customerId: "cust-2", orderId: "ord-2a"));
            await db.SaveChangesAsync();
        }

        using var scope = ctx.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();
        var page = await client.ListOrdersForCustomerAsync("cust-1", new OrderListRequest());

        page.Match<int>(
            onSuccess: p =>
            {
                Assert.Equal(2, p.TotalCount);
                Assert.All(p.Rows, o => Assert.StartsWith("ord-1", o.OrderId));
                return 0;
            },
            onFailure: e => throw new Xunit.Sdk.XunitException(e.Message));
    }

    [Fact]
    public async Task GetOrderDrilldownAsync_assembles_line_items_refunds_benefit_grants()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(initialTenantId: TenantA,
            configureServices: s => s.AddScoped<IPolarReportingClient, EfPolarReportingClient>());

        Guid orderRowId;
        using (var s = ctx.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            var customer = NewCustomer(TenantA, "buyer@x.com", customerId: "cust-1");
            var order = NewOrder(TenantA, customerId: "cust-1", orderId: "ord-detail");
            orderRowId = order.Id;
            db.Customers.Add(customer);
            db.Orders.Add(order);
            db.OrderLineItems.Add(new ReportOrderLineItemEntity
            {
                Id = Guid.NewGuid(), TenantId = TenantA, OrderId = orderRowId,
                ProductId = "prod-1", ProductName = "Thing", Quantity = 2, UnitAmount = 1000, LineTotal = 2000,
            });
            db.OrderRefunds.Add(new ReportOrderRefundEntity
            {
                Id = Guid.NewGuid(), TenantId = TenantA, OrderId = orderRowId,
                PolarRefundId = "ref-1", Amount = 500, Currency = "USD", Reason = "duplicate", CreatedAt = DateTimeOffset.UtcNow,
            });
            db.BenefitGrants.Add(new ReportBenefitGrantEntity
            {
                Id = Guid.NewGuid(), TenantId = TenantA, OrderId = orderRowId,
                PolarGrantId = "bg-1", BenefitId = "ben-1", BenefitName = "Premium", BenefitKind = "license_keys",
                CustomerId = "cust-1", IsGranted = true,
            });
            await db.SaveChangesAsync();
        }

        using var scope = ctx.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();
        var result = await client.GetOrderDrilldownAsync("ord-detail");

        result.Match<int>(
            onSuccess: d =>
            {
                Assert.Equal("ord-detail", d.OrderId);
                Assert.Equal("buyer@x.com", d.CustomerEmail);
                Assert.Single(d.LineItems);
                Assert.Single(d.Refunds);
                Assert.Single(d.BenefitGrants);
                Assert.Equal("prod-1", d.LineItems[0].ProductId);
                Assert.Equal("ref-1", d.Refunds[0].RefundId);
                Assert.Equal("Premium", d.BenefitGrants[0].BenefitName);
                return 0;
            },
            onFailure: e => throw new Xunit.Sdk.XunitException(e.Message));
    }

    [Fact]
    public async Task GetOrderDrilldownAsync_returns_NotFound_for_unknown_order()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(initialTenantId: TenantA,
            configureServices: s => s.AddScoped<IPolarReportingClient, EfPolarReportingClient>());

        using var scope = ctx.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();
        var result = await client.GetOrderDrilldownAsync("nonexistent-order");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ListCustomersAsync_does_not_leak_other_tenants_rows()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(initialTenantId: TenantA,
            configureServices: s => s.AddScoped<IPolarReportingClient, EfPolarReportingClient>());

        // Seed in BOTH tenants. The query filter must scope to TenantA.
        await SeedCustomersAsync(ctx, TenantA, count: 3);
        ctx.SetCurrentTenant(TenantB);
        await SeedCustomersAsync(ctx, TenantB, count: 7);
        ctx.SetCurrentTenant(TenantA);

        using var scope = ctx.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();
        var page = await client.ListCustomersAsync(new CustomerListRequest());

        page.Match<int>(
            onSuccess: p =>
            {
                Assert.Equal(3, p.TotalCount);   // only TenantA's rows; TenantB's 7 are filtered out
                return 0;
            },
            onFailure: e => throw new Xunit.Sdk.XunitException(e.Message));
    }

    /// <summary>
    /// V20-005 acceptance: 10k-customer top-level page in &lt;100ms. Tagged Performance —
    /// not run in the default unit-test gate (timing-sensitive against CI noise).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ListCustomersAsync_top_page_for_10k_tenant_returns_under_100ms()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(initialTenantId: TenantA,
            configureServices: s => s.AddScoped<IPolarReportingClient, EfPolarReportingClient>());

        await SeedCustomersAsync(ctx, TenantA, count: 10_000);

        using var scope = ctx.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IPolarReportingClient>();

        // Warm-up — first query JIT + EF Core compiled-query cache.
        await client.ListCustomersAsync(new CustomerListRequest { Page = 0, PageSize = 50 });

        var sw = Stopwatch.StartNew();
        var result = await client.ListCustomersAsync(new CustomerListRequest { Page = 0, PageSize = 50 });
        sw.Stop();

        Assert.True(result.IsSuccess);
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"Top-level customer page took {sw.ElapsedMilliseconds}ms — V20-005 acceptance is <100ms for 10k customers.");
    }

    // ── Seed helpers ────────────────────────────────────────────────────

    private static async Task SeedCustomersAsync(ReportingTestContext ctx, string tenantId, int count, DateTimeOffset? baseLastOrderAt = null)
    {
        var baseTime = baseLastOrderAt ?? DateTimeOffset.UtcNow;
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        for (var i = 0; i < count; i++)
        {
            db.Customers.Add(new ReportCustomerEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PolarCustomerId = $"cust_{tenantId}_{i:D6}",
                Email = $"user{i}@{tenantId}.test",
                Name = $"User {i}",
                OrderCount = i % 10,
                LifetimeValue = (i % 10) * 1000L,
                Currency = "USD",
                FirstOrderAt = baseTime.AddDays(-i),
                LastOrderAt = baseTime.AddSeconds(-i),
                CreatedAt = baseTime.AddDays(-i),
            });
        }
        await db.SaveChangesAsync();
    }

    private static ReportCustomerEntity NewCustomer(string tenantId, string email, string? customerId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        PolarCustomerId = customerId ?? $"cust_{Guid.NewGuid():N}",
        Email = email,
        OrderCount = 0,
        LifetimeValue = 0,
        Currency = "USD",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static ReportOrderEntity NewOrder(string tenantId, string customerId, string orderId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        PolarOrderId = orderId,
        OrderNumber = orderId,
        Status = "paid",
        Amount = 1000,
        Currency = "USD",
        CustomerId = customerId,
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
