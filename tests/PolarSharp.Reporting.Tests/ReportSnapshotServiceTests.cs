using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;
using PolarSharp.Reporting.Snapshot;
using PolarSharp.Reporting.Tests.Infrastructure;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Tests for the v1.3.F <see cref="ReportSnapshotService"/>. Verifies per-resource
/// ingestion, idempotency (re-running same data doesn't duplicate rows or advance the
/// checkpoint past where Polar actually is), checkpoint advancement (the next run starts
/// where the previous left off), and pre-aggregate refresh (per-customer OrderCount /
/// LifetimeValue + per-order LineItemCount / RefundedAmount).
/// </summary>
public sealed class ReportSnapshotServiceTests
{
    private const string TenantId = ReportingTestContext.DefaultTenantId;

    private static void Configure(IServiceCollection s, FakeReportingApi api)
    {
        s.AddSingleton<IPolarReportingApi>(api);
        s.AddScoped<IReportSnapshotService, ReportSnapshotService>();
    }

    [Fact]
    public async Task Empty_snapshot_run_returns_zero_counts_and_does_not_advance_checkpoints()
    {
        var api = FakeReportingApi.AllEmpty();
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IReportSnapshotService>();
        var report = await svc.RunSnapshotAsync(TenantId);

        Assert.Equal(0, report.EventsIngested);
        Assert.Equal(0, report.OrdersIngested);
        Assert.Equal(0, report.OrderLineItemsIngested);
        Assert.Equal(0, report.SubscriptionsIngested);
        Assert.Equal(0, report.CustomersIngested);

        // Checkpoints are created on first run; null LastPolarId means "nothing ingested yet".
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var checkpoints = await db.Checkpoints.AsNoTracking().ToListAsync();
        Assert.Equal(5, checkpoints.Count);                          // one per resource
        Assert.All(checkpoints, c => Assert.Null(c.LastPolarId));
    }

    [Fact]
    public async Task Events_ingestion_persists_rows_and_advances_checkpoint_to_last_id()
    {
        var events = new[]
        {
            new EventPayload("evt_1", "order.created", DateTimeOffset.UtcNow.AddMinutes(-10), null),
            new EventPayload("evt_2", "order.paid",    DateTimeOffset.UtcNow.AddMinutes(-5),  null),
        };
        var api = FakeReportingApi.WithEvents(events);
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IReportSnapshotService>();
        var report = await svc.RunSnapshotAsync(TenantId);

        Assert.Equal(2, report.EventsIngested);

        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var rows = await db.Events.AsNoTracking().ToListAsync();
        Assert.Equal(2, rows.Count);

        var checkpoint = await db.Checkpoints.AsNoTracking().FirstAsync(c => c.Resource == "events");
        Assert.Equal("evt_2", checkpoint.LastPolarId);
    }

    [Fact]
    public async Task Idempotency_rerunning_with_same_data_after_checkpoint_does_not_duplicate_rows()
    {
        var api = FakeReportingApi.WithEvents([
            new EventPayload("evt_1", "x", DateTimeOffset.UtcNow, null),
        ]);
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IReportSnapshotService>();
        await svc.RunSnapshotAsync(TenantId);

        // Re-run — the API "since=evt_1" returns empty (FakeReportingApi only serves the first
        // call), simulating Polar saying "no new rows after that checkpoint."
        var second = await svc.RunSnapshotAsync(TenantId);
        Assert.Equal(0, second.EventsIngested);

        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        Assert.Equal(1, await db.Events.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Order_ingestion_persists_order_with_line_items_and_refunds()
    {
        var order = new OrderPayload(
            Id: "ord_1",
            Number: "ORD-001",
            CustomerId: "cus_a",
            Status: "paid",
            Amount: 10_000,
            TaxAmount: 800,
            RefundedAmount: 0,
            Currency: "USD",
            InvoiceUrl: "https://polar.sh/invoice/1",
            CreatedAt: DateTimeOffset.UtcNow,
            FulfilledAt: null,
            LineItems:
            [
                new OrderLineItemPayload("prod_x", "Widget", null, 2, 4_000, 8_000, 0, 640),
                new OrderLineItemPayload("prod_y", "Gizmo",  null, 1, 2_000, 2_000, 0, 160),
            ],
            Refunds: [ new OrderRefundPayload("ref_1", 1_000, "USD", "customer_request", DateTimeOffset.UtcNow) ]);

        var api = FakeReportingApi.WithOrders([order]);
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IReportSnapshotService>();
        var report = await svc.RunSnapshotAsync(TenantId);

        Assert.Equal(1, report.OrdersIngested);
        Assert.Equal(2, report.OrderLineItemsIngested);
        Assert.Equal(1, report.OrderRefundsIngested);

        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var savedOrder = await db.Orders.AsNoTracking().FirstAsync();
        Assert.Equal("ord_1", savedOrder.PolarOrderId);
        Assert.Equal(2, savedOrder.LineItemCount);
        Assert.Equal(1_000, savedOrder.RefundedAmount);     // populated by RefreshAggregatesAsync
    }

    [Fact]
    public async Task RefreshAggregates_computes_per_customer_OrderCount_and_LifetimeValue()
    {
        var customer = new CustomerPayload("cus_a", "alice@example.com", "Alice", "USD", DateTimeOffset.UtcNow.AddDays(-30));
        var order1 = MakeOrder("ord_1", "cus_a", amount: 10_000, refunded: 1_000);
        var order2 = MakeOrder("ord_2", "cus_a", amount: 5_000, refunded: 0);

        var api = FakeReportingApi.WithCustomersAndOrders([customer], [order1, order2]);
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IReportSnapshotService>();
        await svc.RunSnapshotAsync(TenantId);

        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var saved = await db.Customers.AsNoTracking().FirstAsync(c => c.PolarCustomerId == "cus_a");
        Assert.Equal(2, saved.OrderCount);
        // LifetimeValue is the sum of (Amount - RefundedAmount) across the customer's orders:
        // (10_000 - 1_000) + (5_000 - 0) = 14_000.
        Assert.Equal(14_000, saved.LifetimeValue);
    }

    [Fact]
    public async Task Customers_ingestion_persists_rows_and_advances_checkpoint()
    {
        var customers = new[]
        {
            new CustomerPayload("cus_a", "a@x.com", "A", "USD", DateTimeOffset.UtcNow),
            new CustomerPayload("cus_b", "b@x.com", "B", "USD", DateTimeOffset.UtcNow),
        };
        var api = FakeReportingApi.WithCustomers(customers);
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IReportSnapshotService>();
        var report = await svc.RunSnapshotAsync(TenantId);

        Assert.Equal(2, report.CustomersIngested);

        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var checkpoint = await db.Checkpoints.AsNoTracking().FirstAsync(c => c.Resource == "customers");
        Assert.Equal("cus_b", checkpoint.LastPolarId);
    }

    [Fact]
    public async Task Order_re_ingestion_replaces_line_items_and_refunds_wholesale()
    {
        var oneItem = new OrderPayload(
            "ord_1", "ORD-001", "cus_a", "paid", 5_000, 0, 0, "USD", null, DateTimeOffset.UtcNow, null,
            LineItems: [ new OrderLineItemPayload("prod_x", "Widget", null, 1, 5_000, 5_000, 0, 0) ],
            Refunds: []);
        var twoItems = oneItem with
        {
            LineItems = [
                new OrderLineItemPayload("prod_x", "Widget", null, 1, 5_000, 5_000, 0, 0),
                new OrderLineItemPayload("prod_y", "Gizmo",  null, 1, 2_000, 2_000, 0, 0),
            ],
            Amount = 7_000,
        };

        var api = FakeReportingApi.WithSequencedOrders([oneItem], [twoItems]);
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IReportSnapshotService>();
        await svc.RunSnapshotAsync(TenantId);                    // first run: one item
        await svc.RunSnapshotAsync(TenantId);                    // second run: two items (replaces wholesale)

        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var lineItems = await db.OrderLineItems.AsNoTracking().ToListAsync();
        Assert.Equal(2, lineItems.Count);                        // not 3 (no leftover from first run)
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static OrderPayload MakeOrder(string id, string customerId, long amount, long refunded) =>
        new(id, id.ToUpper(), customerId, "paid", amount, 0, refunded, "USD", null, DateTimeOffset.UtcNow, null,
            LineItems: [], Refunds: refunded > 0 ? [new OrderRefundPayload($"ref_{id}", refunded, "USD", "customer_request", DateTimeOffset.UtcNow)] : []);

    private sealed class FakeReportingApi : IPolarReportingApi
    {
        private readonly Queue<IReadOnlyList<EventPayload>> _eventPages = [];
        private readonly Queue<IReadOnlyList<OrderPayload>> _orderPages = [];
        private readonly Queue<IReadOnlyList<SubscriptionPayload>> _subPages = [];
        private readonly Queue<IReadOnlyList<CustomerPayload>> _customerPages = [];
        private readonly Queue<IReadOnlyList<BenefitGrantPayload>> _grantPages = [];

        public static FakeReportingApi AllEmpty() => new();

        public static FakeReportingApi WithEvents(IReadOnlyList<EventPayload> events)
        {
            var api = new FakeReportingApi();
            api._eventPages.Enqueue(events);
            return api;
        }

        public static FakeReportingApi WithOrders(IReadOnlyList<OrderPayload> orders)
        {
            var api = new FakeReportingApi();
            api._orderPages.Enqueue(orders);
            return api;
        }

        public static FakeReportingApi WithCustomers(IReadOnlyList<CustomerPayload> customers)
        {
            var api = new FakeReportingApi();
            api._customerPages.Enqueue(customers);
            return api;
        }

        public static FakeReportingApi WithCustomersAndOrders(IReadOnlyList<CustomerPayload> customers, IReadOnlyList<OrderPayload> orders)
        {
            var api = new FakeReportingApi();
            api._customerPages.Enqueue(customers);
            api._orderPages.Enqueue(orders);
            return api;
        }

        public static FakeReportingApi WithSequencedOrders(params IReadOnlyList<OrderPayload>[] pages)
        {
            var api = new FakeReportingApi();
            foreach (var page in pages) api._orderPages.Enqueue(page);
            return api;
        }

        public Task<Result<IReadOnlyList<EventPayload>, PolarReportingApiError>> FetchEventsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            NextPage(_eventPages);

        public Task<Result<IReadOnlyList<OrderPayload>, PolarReportingApiError>> FetchOrdersSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            NextPage(_orderPages);

        public Task<Result<IReadOnlyList<SubscriptionPayload>, PolarReportingApiError>> FetchSubscriptionsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            NextPage(_subPages);

        public Task<Result<IReadOnlyList<CustomerPayload>, PolarReportingApiError>> FetchCustomersSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            NextPage(_customerPages);

        public Task<Result<IReadOnlyList<BenefitGrantPayload>, PolarReportingApiError>> FetchBenefitGrantsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            NextPage(_grantPages);

        // V20-005 Phase 1A: the 7 new interface methods are stubs in the test fake. Until
        // tests for the live ingestion of those resources land (Phase 1B+), every call here
        // returns an empty success page — matching the production stub behavior.
        public Task<Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>> FetchBenefitsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            Empty<BenefitPayload>();
        public Task<Result<IReadOnlyList<DiscountPayload>, PolarReportingApiError>> FetchDiscountsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            Empty<DiscountPayload>();
        public Task<Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>> FetchCheckoutLinksSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            Empty<CheckoutLinkPayload>();
        public Task<Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>> FetchProductsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            Empty<ProductPayload>();
        public Task<Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>> FetchLicenseKeysSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            Empty<LicenseKeyPayload>();
        public Task<Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>> FetchMetersSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            Empty<MeterPayload>();
        public Task<Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>> FetchCustomerMetersSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
            Empty<CustomerMeterPayload>();

        private static Task<Result<IReadOnlyList<T>, PolarReportingApiError>> Empty<T>() =>
            Task.FromResult(Result<IReadOnlyList<T>, PolarReportingApiError>.Success((IReadOnlyList<T>)Array.Empty<T>()));

        private static Task<Result<IReadOnlyList<T>, PolarReportingApiError>> NextPage<T>(Queue<IReadOnlyList<T>> queue)
        {
            var page = queue.Count > 0 ? queue.Dequeue() : (IReadOnlyList<T>)Array.Empty<T>();
            return Task.FromResult(Result<IReadOnlyList<T>, PolarReportingApiError>.Success(page));
        }
    }
}
