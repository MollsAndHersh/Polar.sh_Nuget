using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using static PolarSharp.EcommerceStoreManagement.Tests.Infrastructure.ResultTestExtensions;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Tests for the v1.3.C <see cref="RefundService"/>. Verifies input validation
/// (CommentRequired-for-Other, non-positive amounts, missing currency), Polar-error mapping,
/// and that every successful refund lands in the audit log.
/// </summary>
public sealed class RefundServiceTests
{
    private static void Configure(IServiceCollection s, FakeRefundsApi api)
    {
        s.AddSingleton<IPolarRefundsApi>(api);
        s.AddSingleton<IAuditLogActorProvider>(new TestActorProvider("actor@test.local"));
        s.AddScoped<IRefundService, RefundService>();
    }

    [Fact]
    public async Task IssueFullRefundAsync_with_valid_input_returns_RefundResult_and_writes_audit_entry()
    {
        var api = FakeRefundsApi.SucceedsWith(new RefundApiResponse("ref_abc", 5000, "USD", RefundReason.CustomerRequest, null, DateTimeOffset.UtcNow));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IRefundService>();
        var result = await svc.IssueFullRefundAsync("ord_xyz", RefundReason.CustomerRequest, comment: null);

        var refund = result.ValueOrThrow();
        Assert.Equal("ref_abc", refund.RefundId);
        Assert.Equal(5000, refund.AmountRefunded);
        Assert.Equal("USD", refund.Currency);

        // Audit entry persisted.
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var audit = await db.AuditLog.ToListAsync();
        Assert.Single(audit);
        Assert.Equal("Refund", audit[0].EntityType);
        Assert.Equal("actor@test.local", audit[0].ActorEmail);
        Assert.Equal(AuditAction.Create, audit[0].Action);
    }

    [Fact]
    public async Task IssueFullRefundAsync_with_Other_reason_and_no_comment_returns_CommentRequired()
    {
        var api = FakeRefundsApi.SucceedsWith(new RefundApiResponse("never", 0, "USD", RefundReason.Other, null, DateTimeOffset.UtcNow));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IRefundService>();
        var result = await svc.IssueFullRefundAsync("ord_xyz", RefundReason.Other, comment: "  ");

        Assert.Equal(RefundErrorKind.CommentRequired, result.ErrorOrThrow().Kind);
        Assert.Empty(api.CreateRefundCalls);                  // never reaches Polar
    }

    [Fact]
    public async Task IssuePartialRefundAsync_with_zero_amount_returns_AmountExceedsRefundable()
    {
        var api = FakeRefundsApi.SucceedsWith(new RefundApiResponse("never", 0, "USD", RefundReason.CustomerRequest, null, DateTimeOffset.UtcNow));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IRefundService>();
        var result = await svc.IssuePartialRefundAsync("ord_xyz", amount: 0, currency: "USD", RefundReason.CustomerRequest, comment: null);

        Assert.Equal(RefundErrorKind.AmountExceedsRefundable, result.ErrorOrThrow().Kind);
        Assert.Empty(api.CreateRefundCalls);
    }

    [Fact]
    public async Task IssuePartialRefundAsync_with_empty_currency_returns_CurrencyMismatch()
    {
        var api = FakeRefundsApi.SucceedsWith(new RefundApiResponse("never", 100, "USD", RefundReason.CustomerRequest, null, DateTimeOffset.UtcNow));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IRefundService>();
        var result = await svc.IssuePartialRefundAsync("ord_xyz", amount: 100, currency: "   ", RefundReason.CustomerRequest, comment: null);

        Assert.Equal(RefundErrorKind.CurrencyMismatch, result.ErrorOrThrow().Kind);
    }

    [Fact]
    public async Task Polar_OrderNotFound_maps_to_public_OrderNotFound_error_and_no_audit_entry_persisted()
    {
        var api = FakeRefundsApi.FailsWith(new RefundApiError(RefundApiErrorKind.OrderNotFound, "Unknown order"));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IRefundService>();
        var result = await svc.IssueFullRefundAsync("ord_missing", RefundReason.CustomerRequest, comment: null);

        Assert.Equal(RefundErrorKind.OrderNotFound, result.ErrorOrThrow().Kind);
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        Assert.Empty(await db.AuditLog.ToListAsync());        // no audit entry when refund fails
    }

    [Fact]
    public async Task Polar_AlreadyFullyRefunded_maps_to_public_AlreadyFullyRefunded()
    {
        var api = FakeRefundsApi.FailsWith(new RefundApiError(RefundApiErrorKind.AlreadyFullyRefunded, "Already refunded"));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IRefundService>();
        var result = await svc.IssueFullRefundAsync("ord_already", RefundReason.CustomerRequest, comment: null);

        Assert.Equal(RefundErrorKind.AlreadyFullyRefunded, result.ErrorOrThrow().Kind);
    }

    [Fact]
    public async Task ListForOrderAsync_returns_Polar_refund_history()
    {
        var api = FakeRefundsApi.WithListResult(
        [
            new RefundApiResponse("ref_1", 1000, "USD", RefundReason.CustomerRequest, null, DateTimeOffset.UtcNow.AddDays(-1)),
            new RefundApiResponse("ref_2", 500, "USD", RefundReason.DuplicateCharge, "Posted twice", DateTimeOffset.UtcNow),
        ]);
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IRefundService>();
        var result = await svc.ListForOrderAsync("ord_xyz");

        var rows = result.ValueOrThrow();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.RefundId == "ref_1" && r.Amount == 1000);
        Assert.Contains(rows, r => r.RefundId == "ref_2" && r.Reason == RefundReason.DuplicateCharge);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class FakeRefundsApi : IPolarRefundsApi
    {
        public List<RefundApiRequest> CreateRefundCalls { get; } = [];
        private readonly Result<RefundApiResponse, RefundApiError> _createResult;
        private readonly IReadOnlyList<RefundApiResponse> _listResult;

        private FakeRefundsApi(Result<RefundApiResponse, RefundApiError> createResult, IReadOnlyList<RefundApiResponse>? listResult = null)
        {
            _createResult = createResult;
            _listResult = listResult ?? [];
        }

        public static FakeRefundsApi SucceedsWith(RefundApiResponse response) =>
            new(Result<RefundApiResponse, RefundApiError>.Success(response));

        public static FakeRefundsApi FailsWith(RefundApiError error) =>
            new(Result<RefundApiResponse, RefundApiError>.Failure(error));

        public static FakeRefundsApi WithListResult(IReadOnlyList<RefundApiResponse> rows) =>
            new(Result<RefundApiResponse, RefundApiError>.Failure(new RefundApiError(RefundApiErrorKind.UnexpectedFailure, "unused")), rows);

        public Task<Result<RefundApiResponse, RefundApiError>> CreateRefundAsync(RefundApiRequest request, CancellationToken ct)
        {
            CreateRefundCalls.Add(request);
            return Task.FromResult(_createResult);
        }

        public Task<Result<IReadOnlyList<RefundApiResponse>, RefundApiError>> ListRefundsForOrderAsync(string polarOrderId, CancellationToken ct) =>
            Task.FromResult(Result<IReadOnlyList<RefundApiResponse>, RefundApiError>.Success(_listResult));
    }

    private sealed class TestActorProvider(string email) : IAuditLogActorProvider
    {
        public AuditActor GetCurrentActor() => new(Guid.NewGuid(), email, IsAppMasterAdmin: false, CurrentTenantId: null);
    }
}
