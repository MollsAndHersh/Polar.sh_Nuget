using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.Reporting.Snapshot;

namespace PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

/// <summary>
/// V20-005 Phase 2 implementation. Default <see cref="IReportSnapshotTrigger"/> — drives
/// per-tenant snapshots on demand and on a heartbeat-gated cron, with in-flight
/// deduplication, manual-refresh debounce, idle timeout, and an event-channel for UI
/// freshness updates.
/// </summary>
/// <remarks>
/// <para>
/// Threading: every public method is thread-safe. State lives in a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> of per-tenant <see cref="TenantState"/>;
/// each <see cref="TenantState"/> carries a <see cref="SemaphoreSlim"/>(1,1) for in-flight
/// dedup so concurrent triggers on the same tenant serialize without blocking other tenants.
/// </para>
/// <para>
/// Lifetime: registered as <see cref="ServiceLifetime.Singleton"/> because per-tenant timers
/// + last-snapshot-at timestamps + channel state must survive across request scopes. Inside
/// the snapshot run, the orchestrator opens a transient DI scope per tick so that
/// <see cref="PolarReportingDbContext"/> (Scoped) and <see cref="IReportSnapshotService"/>
/// resolve cleanly with the Finbuckle tenant context hydrated by
/// <see cref="IPolarTenantScopeInitializer"/>.
/// </para>
/// </remarks>
internal sealed class PerTenantSnapshotOrchestrator : IReportSnapshotTrigger, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<PerTenantSnapshotOrchestrator> _logger;
    private readonly SnapshotTriggerOptions _options;
    private readonly ConcurrentDictionary<string, TenantState> _tenants = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<SnapshotCompletedEvent> _completedChannel;
    private readonly CancellationTokenSource _disposalCts = new();

    /// <summary>Initializes the orchestrator.</summary>
    public PerTenantSnapshotOrchestrator(
        IServiceScopeFactory scopeFactory,
        TimeProvider time,
        ILogger<PerTenantSnapshotOrchestrator> logger,
        IOptions<SnapshotTriggerOptions> options)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _completedChannel = Channel.CreateBounded<SnapshotCompletedEvent>(new BoundedChannelOptions(_options.CompletionChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,    // never block snapshot runs on slow UI subscribers
            SingleReader = false,                            // multiple UI subscribers via CompletedEvents()
            SingleWriter = false,
        });
    }

    /// <inheritdoc/>
    public async Task<SnapshotCompletedEvent> TriggerImmediateAsync(string tenantId, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentException.ThrowIfNullOrEmpty(reason);

        var state = _tenants.GetOrAdd(tenantId, t => new TenantState(t));

        // Manual-refresh debounce: when reason=="ManualRefresh" and a snapshot completed
        // within the debounce window, replay the cached completion instead of firing again.
        if (reason.Equals("ManualRefresh", StringComparison.OrdinalIgnoreCase)
            && state.LastCompletion is { } cached
            && _time.GetUtcNow() - cached.CompletedAt < _options.ManualRefreshDebounce)
        {
            _logger.LogDebug("ManualRefresh debounced for tenant {TenantId} — replaying cached completion at {At}.",
                tenantId, cached.CompletedAt);
            return cached;
        }

        return await RunUnderSemaphoreAsync(state, reason, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StartPeriodicAsync(string tenantId, TimeSpan interval)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        if (interval <= TimeSpan.Zero) interval = _options.DefaultInterval;

        var state = _tenants.GetOrAdd(tenantId, t => new TenantState(t));
        state.Heartbeat(_time.GetUtcNow());
        state.ConfigurePeriodic(interval);

        // Stop any existing timer so we can rebuild with the (possibly new) interval.
        if (state.Timer is { } old) await old.DisposeAsync().ConfigureAwait(false);
        // First tick immediate; subsequent every `interval`.
        state.Timer = new Timer(_ => _ = OnTimerTickAsync(state), state: null, TimeSpan.Zero, interval);
    }

    private Task OnTimerTickAsync(TenantState state)
    {
        var ctx = (self: this, state);
        // Idle-timeout check before firing — if the tenant hasn't heartbeated in the idle
        // window, stop polling. Cheap pre-check before taking the semaphore.
        var now = _time.GetUtcNow();
        if (now - ctx.state.LastHeartbeat > _options.IdleTimeout)
        {
            _logger.LogDebug("Idle-timeout reached for tenant {TenantId} (last heartbeat {Last}); stopping periodic poll.",
                ctx.state.TenantId, ctx.state.LastHeartbeat);
            _ = StopPeriodicAsync(ctx.state.TenantId);
            return Task.CompletedTask;
        }

        // Fire-and-forget the snapshot. Errors are captured into the completion event;
        // they MUST NOT propagate out of the Timer callback.
        _ = Task.Run(async () =>
        {
            try { await RunUnderSemaphoreAsync(ctx.state, "Periodic", _disposalCts.Token).ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Periodic snapshot tick for tenant {TenantId} threw outside the wrapper — unexpected; suppressing.", ctx.state.TenantId);
            }
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopPeriodicAsync(string tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        // Full cleanup — dispose the timer AND remove the entry from the dictionary so the
        // SemaphoreSlim it owns gets garbage-collected, not held forever. Without this, a
        // host that sees many short-lived tenant sessions accumulates per-tenant state
        // unboundedly. See V20-020-MEMORY-LEAK-AUDIT.md for the broader audit context.
        if (_tenants.TryRemove(tenantId, out var state))
        {
            if (state.Timer is { } timer) await timer.DisposeAsync().ConfigureAwait(false);
            state.Semaphore.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Heartbeat(string tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        if (_tenants.TryGetValue(tenantId, out var state))
        {
            state.Heartbeat(_time.GetUtcNow());
        }
    }

    /// <inheritdoc/>
    public DateTimeOffset? GetLastSnapshotAt(string tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        return _tenants.TryGetValue(tenantId, out var state) ? state.LastCompletion?.CompletedAt : null;
    }

    /// <inheritdoc/>
    public TimeSpan? GetTimeUntilNextSnapshot(string tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        if (!_tenants.TryGetValue(tenantId, out var state) || state.Interval is not { } interval
            || state.LastCompletion is null) return null;

        var elapsed = _time.GetUtcNow() - state.LastCompletion.CompletedAt;
        var remaining = interval - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SnapshotCompletedEvent> CompletedEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var evt in _completedChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    /// <summary>
    /// Runs one snapshot under the tenant's in-flight semaphore (1 permit). Concurrent
    /// callers wait for the in-flight snapshot and receive the same completion event.
    /// </summary>
    private async Task<SnapshotCompletedEvent> RunUnderSemaphoreAsync(TenantState state, string reason, CancellationToken ct)
    {
        await state.Semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Once we hold the semaphore, check if another caller just completed — if their
            // completion is fresher than the debounce window, replay it. Same dedup as the
            // outer debounce; this catches a race where two callers waited together.
            if (reason.Equals("ManualRefresh", StringComparison.OrdinalIgnoreCase)
                && state.LastCompletion is { } cached
                && _time.GetUtcNow() - cached.CompletedAt < _options.ManualRefreshDebounce)
            {
                return cached;
            }

            var evt = await RunSnapshotAsync(state.TenantId, reason, ct).ConfigureAwait(false);
            state.LastCompletion = evt;
            await _completedChannel.Writer.WriteAsync(evt, ct).ConfigureAwait(false);
            return evt;
        }
        finally
        {
            state.Semaphore.Release();
        }
    }

    /// <summary>
    /// Runs <see cref="IReportSnapshotService.RunSnapshotAsync"/> for the tenant under a
    /// fresh DI scope with Finbuckle tenant context hydrated via
    /// <see cref="IPolarTenantScopeInitializer"/>. Captures success/failure into a typed
    /// <see cref="SnapshotCompletedEvent"/> — exceptions never propagate.
    /// </summary>
    private async Task<SnapshotCompletedEvent> RunSnapshotAsync(string tenantId, string reason, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var now = _time.GetUtcNow();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<IPolarTenantScopeInitializer>();
            // Two-phase: async resolve, then SYNC apply in this frame so the AsyncLocal
            // mutation persists past the next await. See IPolarTenantScopeInitializer
            // remarks for the AsyncLocal scoping rationale.
            var tenant = await initializer.ResolveTenantAsync(tenantId, ct).ConfigureAwait(false);
            if (tenant is null)
            {
                sw.Stop();
                return new SnapshotCompletedEvent
                {
                    TenantId = tenantId,
                    CompletedAt = _time.GetUtcNow(),
                    Reason = reason,
                    Success = false,
                    ResourcesIngested = 0,
                    RowsIngested = 0,
                    Duration = sw.Elapsed,
                    ErrorMessage = $"Tenant '{tenantId}' not found in store.",
                };
            }
            scope.ServiceProvider.SetCurrentTenant(tenant);

            var snapshot = scope.ServiceProvider.GetRequiredService<IReportSnapshotService>();
            var report = await snapshot.RunSnapshotAsync(tenantId, ct).ConfigureAwait(false);
            sw.Stop();

            var totals = TallyReport(report);
            return new SnapshotCompletedEvent
            {
                TenantId = tenantId,
                CompletedAt = _time.GetUtcNow(),
                Reason = reason,
                Success = true,
                ResourcesIngested = totals.NonZeroResources,
                RowsIngested = totals.TotalRows,
                Duration = sw.Elapsed,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger.LogError(ex, "Snapshot tick failed for tenant {TenantId} (reason={Reason}).", tenantId, reason);
            return new SnapshotCompletedEvent
            {
                TenantId = tenantId,
                CompletedAt = _time.GetUtcNow(),
                Reason = reason,
                Success = false,
                ResourcesIngested = 0,
                RowsIngested = 0,
                Duration = sw.Elapsed,
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}",
            };
        }
    }

    /// <summary>Counts non-zero resource ingestions + total rows from the per-tick snapshot report.</summary>
    private static (int NonZeroResources, int TotalRows) TallyReport(SnapshotReport r)
    {
        int total = r.EventsIngested + r.OrdersIngested + r.OrderLineItemsIngested + r.OrderRefundsIngested
                  + r.SubscriptionsIngested + r.CustomersIngested + r.BenefitGrantsIngested
                  + r.ProductsIngested + r.CustomerMetersIngested + r.LicenseKeysIngested
                  + r.BenefitsIngested + r.MetersIngested + r.CheckoutLinksIngested + r.DiscountsIngested;
        int nonZero = (r.EventsIngested > 0 ? 1 : 0) + (r.OrdersIngested > 0 ? 1 : 0)
                    + (r.SubscriptionsIngested > 0 ? 1 : 0) + (r.CustomersIngested > 0 ? 1 : 0)
                    + (r.BenefitGrantsIngested > 0 ? 1 : 0) + (r.ProductsIngested > 0 ? 1 : 0)
                    + (r.CustomerMetersIngested > 0 ? 1 : 0) + (r.LicenseKeysIngested > 0 ? 1 : 0)
                    + (r.BenefitsIngested > 0 ? 1 : 0) + (r.MetersIngested > 0 ? 1 : 0)
                    + (r.CheckoutLinksIngested > 0 ? 1 : 0) + (r.DiscountsIngested > 0 ? 1 : 0);
        return (nonZero, total);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _disposalCts.CancelAsync().ConfigureAwait(false);
        foreach (var state in _tenants.Values)
        {
            if (state.Timer is { } timer) await timer.DisposeAsync().ConfigureAwait(false);
            state.Semaphore.Dispose();
        }
        _completedChannel.Writer.TryComplete();
        _disposalCts.Dispose();
    }

    /// <summary>Per-tenant orchestrator state. Mutated under the parent's lock-free
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/>; per-tenant mutation serialized by
    /// <see cref="Semaphore"/>.</summary>
    private sealed class TenantState(string tenantId)
    {
        public string TenantId { get; } = tenantId;
        public Timer? Timer { get; set; }
        public TimeSpan? Interval { get; set; }
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public DateTimeOffset LastHeartbeat { get; private set; } = DateTimeOffset.MinValue;
        public SnapshotCompletedEvent? LastCompletion { get; set; }

        public void Heartbeat(DateTimeOffset at) => LastHeartbeat = at;
        public void ConfigurePeriodic(TimeSpan interval) => Interval = interval;
    }
}
