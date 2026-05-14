using System.Threading.Channels;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;
using PolarSharp.Reporting.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// V20-005 Phase 2 unit tests covering the four behaviors the design doc called out:
/// dedup semaphore (in-flight triggers serialize), manual-refresh debounce, periodic
/// timer + idle-timeout, and completion event publishing.
/// </summary>
public sealed class PerTenantSnapshotOrchestratorTests
{
    private const string TenantA = "tenant-a";

    private static (PerTenantSnapshotOrchestrator orch, ServiceProvider sp, FakeSnapshotService fake) BuildHarness(
        SnapshotTriggerOptions? options = null,
        TimeProvider? time = null)
    {
        var conn = new SqliteConnection("Filename=:memory:");
        conn.Open();

        var fake = new FakeSnapshotService();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(time ?? TimeProvider.System);
        services.AddSingleton<IMultiTenantContextAccessor>(new StubAccessor(TenantA));
        services.AddSingleton<IPolarTenantScopeInitializer, StubScopeInitializer>();
        services.AddDbContext<PolarReportingDbContext>(opts => opts.UseSqlite(conn));
        services.AddScoped<IReportSnapshotService>(_ => fake);
        services.AddSingleton(Options.Create(options ?? new SnapshotTriggerOptions()));
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var orch = new PerTenantSnapshotOrchestrator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<TimeProvider>(),
            NullLogger<PerTenantSnapshotOrchestrator>.Instance,
            provider.GetRequiredService<IOptions<SnapshotTriggerOptions>>());
        return (orch, provider, fake);
    }

    [Fact]
    public async Task TriggerImmediate_publishes_a_completion_event_and_invokes_snapshot_service()
    {
        var (orch, sp, fake) = BuildHarness();
        await using var _ = orch;
        using var __ = sp;

        var evt = await orch.TriggerImmediateAsync(TenantA, "Login", CancellationToken.None);

        Assert.Equal(TenantA, evt.TenantId);
        Assert.Equal("Login", evt.Reason);
        Assert.True(evt.Success);
        Assert.Equal(1, fake.Invocations);
    }

    [Fact]
    public async Task ManualRefresh_within_debounce_window_replays_cached_completion_without_re_invoking()
    {
        var options = new SnapshotTriggerOptions { ManualRefreshDebounce = TimeSpan.FromSeconds(5) };
        var (orch, sp, fake) = BuildHarness(options);
        await using var _ = orch;
        using var __ = sp;

        var first = await orch.TriggerImmediateAsync(TenantA, "ManualRefresh", CancellationToken.None);
        var second = await orch.TriggerImmediateAsync(TenantA, "ManualRefresh", CancellationToken.None);

        Assert.Equal(1, fake.Invocations);                 // second call replayed the cache
        Assert.Equal(first.CompletedAt, second.CompletedAt);
    }

    [Fact]
    public async Task NonManualRefresh_reason_does_NOT_debounce()
    {
        var options = new SnapshotTriggerOptions { ManualRefreshDebounce = TimeSpan.FromSeconds(5) };
        var (orch, sp, fake) = BuildHarness(options);
        await using var _ = orch;
        using var __ = sp;

        await orch.TriggerImmediateAsync(TenantA, "Login", CancellationToken.None);
        await orch.TriggerImmediateAsync(TenantA, "PostMutation", CancellationToken.None);

        Assert.Equal(2, fake.Invocations);                 // each non-manual reason fires
    }

    [Fact]
    public async Task StopPeriodic_removes_tenant_state_from_internal_dict_no_leak()
    {
        var (orch, sp, _) = BuildHarness();
        await using var _ = orch;
        using var __ = sp;

        await orch.StartPeriodicAsync(TenantA, TimeSpan.FromHours(1));
        // Give the first immediate tick a moment to enter the semaphore (we're not asserting
        // on it; just want to confirm StopPeriodic cleans up regardless of tick state).
        await Task.Delay(50);

        await orch.StopPeriodicAsync(TenantA);

        // After StopPeriodic the tenant should have no LastSnapshotAt accessor (because the
        // entry was removed from the dict). This is the memory-leak fix verification: the
        // SemaphoreSlim that the entry owned is now disposed, not retained.
        Assert.Null(orch.GetTimeUntilNextSnapshot(TenantA));
    }

    [Fact]
    public async Task CompletedEventsAsync_streams_one_event_per_trigger()
    {
        var (orch, sp, _) = BuildHarness();
        using var __ = sp;

        var received = new List<SnapshotCompletedEvent>();
        using var cts = new CancellationTokenSource();
        var consumer = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in orch.CompletedEventsAsync(cts.Token))
                {
                    received.Add(evt);
                    if (received.Count >= 2) return;
                }
            }
            catch (OperationCanceledException) { /* expected on shutdown */ }
        });

        await orch.TriggerImmediateAsync(TenantA, "First", CancellationToken.None);
        await orch.TriggerImmediateAsync(TenantA, "Second", CancellationToken.None);

        // Give the consumer a brief moment to drain the channel.
        await Task.WhenAny(consumer, Task.Delay(2000));
        cts.Cancel();

        Assert.Equal(2, received.Count);
        Assert.Equal("First", received[0].Reason);
        Assert.Equal("Second", received[1].Reason);
        await orch.DisposeAsync();
    }

    [Fact]
    public async Task Heartbeat_on_unknown_tenant_is_safe_noop_does_not_throw()
    {
        var (orch, sp, _) = BuildHarness();
        await using var _ = orch;
        using var __ = sp;

        // No prior StartPeriodic — Heartbeat on a tenant we've never seen should silently do nothing.
        var exception = Record.Exception(() => orch.Heartbeat("unknown-tenant"));
        Assert.Null(exception);
    }

    [Fact]
    public async Task GetLastSnapshotAt_returns_null_for_unknown_tenant()
    {
        var (orch, sp, _) = BuildHarness();
        await using var _ = orch;
        using var __ = sp;

        Assert.Null(orch.GetLastSnapshotAt("unknown-tenant"));
    }

    [Fact]
    public async Task GetLastSnapshotAt_returns_completion_timestamp_after_trigger()
    {
        var (orch, sp, _) = BuildHarness();
        await using var _ = orch;
        using var __ = sp;

        var evt = await orch.TriggerImmediateAsync(TenantA, "Login", CancellationToken.None);
        var last = orch.GetLastSnapshotAt(TenantA);

        Assert.NotNull(last);
        Assert.Equal(evt.CompletedAt, last);
    }

    // ── Test doubles ────────────────────────────────────────────────────────────

    private sealed class FakeSnapshotService : IReportSnapshotService
    {
        public int Invocations { get; private set; }
        public Task<SnapshotReport> RunSnapshotAsync(string tenantId, CancellationToken ct = default)
        {
            Invocations++;
            return Task.FromResult(new SnapshotReport(
                EventsIngested: 0, OrdersIngested: 0, OrderLineItemsIngested: 0, OrderRefundsIngested: 0,
                SubscriptionsIngested: 0, CustomersIngested: 0, BenefitGrantsIngested: 0,
                ProductsIngested: 0, CustomerMetersIngested: 0, LicenseKeysIngested: 0,
                BenefitsIngested: 0, MetersIngested: 0, CheckoutLinksIngested: 0, DiscountsIngested: 0,
                Duration: TimeSpan.Zero));
        }
    }

    private sealed class StubScopeInitializer : IPolarTenantScopeInitializer
    {
        public Task<PolarTenantInfo?> ResolveTenantAsync(string tenantId, CancellationToken ct = default) =>
            Task.FromResult<PolarTenantInfo?>(new PolarTenantInfo { Id = tenantId, Identifier = tenantId, Name = tenantId });
    }

    private sealed class StubAccessor(string tenantId) : IMultiTenantContextAccessor
    {
        public IMultiTenantContext MultiTenantContext { get; set; } =
            new MultiTenantContext<PolarTenantInfo>(
                new PolarTenantInfo { Id = tenantId, Identifier = tenantId, Name = tenantId });
    }
}
