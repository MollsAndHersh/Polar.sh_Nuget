using Finbuckle.MultiTenant.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.Tests.Lifecycle;

/// <summary>
/// Tests for <see cref="DefaultTenantStatusService"/> — the canonical lifecycle state-change
/// service. Covers state transitions, idempotent no-ops, the unverified-email suspension
/// gate, store-write failure handling, MediatR-publish failure handling, notification
/// payload composition, and clock-injection.
/// </summary>
public sealed class DefaultTenantStatusServiceTests
{
    // --- SuspendAsync ----------------------------------------------------------------

    [Fact]
    public async Task SuspendAsync_changes_status_from_Active_to_Suspended_and_publishes_notification()
    {
        var tenant = NewTenant(status: TenantStatus.Active, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator);

        var result = await sut.SuspendAsync(tenant.TenantId, reason: "Billing dispute");

        Assert.True(result.Success, result.FailureReason);
        Assert.False(result.WasIdempotentNoOp);
        Assert.Equal(TenantStatus.Active, result.PreviousStatus);
        Assert.Equal(TenantStatus.Suspended, result.NewStatus);
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.Equal(1, mediator.PublishCalls);
    }

    [Fact]
    public async Task SuspendAsync_is_idempotent_when_already_Suspended()
    {
        var tenant = NewTenant(status: TenantStatus.Suspended, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator);

        var result = await sut.SuspendAsync(tenant.TenantId, reason: "Already suspended retry");

        Assert.True(result.Success);
        Assert.True(result.WasIdempotentNoOp);
        Assert.Equal(TenantStatus.Suspended, result.PreviousStatus);
        Assert.Equal(TenantStatus.Suspended, result.NewStatus);
        // Notification MUST NOT fire for an idempotent no-op.
        Assert.Equal(0, mediator.PublishCalls);
        // Store update MUST NOT fire either.
        Assert.Equal(0, store.UpdateCalls);
    }

    [Fact]
    public async Task SuspendAsync_refuses_when_email_unverified_and_RequireVerifiedEmailForSuspension_is_true()
    {
        var tenant = NewTenant(status: TenantStatus.Active, emailVerified: false);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(
            store,
            mediator,
            new TenantStatusServiceOptions
            {
                RequireVerifiedEmailForSuspension = true,
                SuspendUnverifiedTenantsAnyway = false,
            });

        var result = await sut.SuspendAsync(tenant.TenantId, reason: "TOS violation");

        Assert.False(result.Success);
        Assert.False(result.WasIdempotentNoOp);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("unverified", result.FailureReason!, StringComparison.OrdinalIgnoreCase);
        // Tenant entity stays Active.
        Assert.Equal(TenantStatus.Active, tenant.Status);
        // No store write attempted.
        Assert.Equal(0, store.UpdateCalls);
        // No notification fired.
        Assert.Equal(0, mediator.PublishCalls);
    }

    [Fact]
    public async Task SuspendAsync_proceeds_when_email_unverified_and_SuspendUnverifiedTenantsAnyway_is_true()
    {
        // Explicit-override path: documented escape hatch for operators that need to suspend
        // regardless of verification state (e.g., fraud response).
        var tenant = NewTenant(status: TenantStatus.Active, emailVerified: false);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(
            store,
            mediator,
            new TenantStatusServiceOptions
            {
                RequireVerifiedEmailForSuspension = true,
                SuspendUnverifiedTenantsAnyway = true,
            });

        var result = await sut.SuspendAsync(tenant.TenantId, reason: "Fraud response");

        Assert.True(result.Success, result.FailureReason);
        Assert.Equal(TenantStatus.Suspended, result.NewStatus);
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.Equal(1, mediator.PublishCalls);
    }

    [Fact]
    public async Task SuspendAsync_proceeds_when_email_verified()
    {
        var tenant = NewTenant(status: TenantStatus.Active, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator);

        var result = await sut.SuspendAsync(tenant.TenantId, reason: "Verified suspension");

        Assert.True(result.Success, result.FailureReason);
        Assert.Equal(TenantStatus.Suspended, result.NewStatus);
        Assert.Equal(1, mediator.PublishCalls);
    }

    // --- ReactivateAsync -------------------------------------------------------------

    [Fact]
    public async Task ReactivateAsync_changes_status_from_Suspended_to_Active()
    {
        var tenant = NewTenant(status: TenantStatus.Suspended, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator);

        var result = await sut.ReactivateAsync(tenant.TenantId);

        Assert.True(result.Success);
        Assert.Equal(TenantStatus.Suspended, result.PreviousStatus);
        Assert.Equal(TenantStatus.Active, result.NewStatus);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(1, mediator.PublishCalls);
    }

    [Fact]
    public async Task ReactivateAsync_changes_status_from_Inactive_to_Active()
    {
        var tenant = NewTenant(status: TenantStatus.Inactive, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator);

        var result = await sut.ReactivateAsync(tenant.TenantId);

        Assert.True(result.Success);
        Assert.Equal(TenantStatus.Inactive, result.PreviousStatus);
        Assert.Equal(TenantStatus.Active, result.NewStatus);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(1, mediator.PublishCalls);
    }

    [Fact]
    public async Task ReactivateAsync_is_idempotent_when_already_Active()
    {
        var tenant = NewTenant(status: TenantStatus.Active, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator);

        var result = await sut.ReactivateAsync(tenant.TenantId);

        Assert.True(result.Success);
        Assert.True(result.WasIdempotentNoOp);
        Assert.Equal(0, mediator.PublishCalls);
        Assert.Equal(0, store.UpdateCalls);
    }

    // --- DeactivateAsync -------------------------------------------------------------

    [Fact]
    public async Task DeactivateAsync_changes_status_from_Active_to_Inactive_and_publishes_notification()
    {
        var tenant = NewTenant(status: TenantStatus.Active, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator);

        var result = await sut.DeactivateAsync(tenant.TenantId, reason: "Tenant-initiated closure");

        Assert.True(result.Success);
        Assert.Equal(TenantStatus.Active, result.PreviousStatus);
        Assert.Equal(TenantStatus.Inactive, result.NewStatus);
        Assert.Equal(TenantStatus.Inactive, tenant.Status);
        Assert.Equal(1, mediator.PublishCalls);
    }

    // --- DeleteAsync -----------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_changes_status_to_Deleted_from_Active()
    {
        var tenant = NewTenant(status: TenantStatus.Active, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator);

        var result = await sut.DeleteAsync(tenant.TenantId, reason: "Retention expired");

        Assert.True(result.Success);
        Assert.Equal(TenantStatus.Deleted, result.NewStatus);
        Assert.Equal(TenantStatus.Deleted, tenant.Status);
    }

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Inactive)]
    public async Task DeleteAsync_publishes_notification_with_PreviousStatus_correctly_set(TenantStatus previousStatus)
    {
        var tenant = NewTenant(status: previousStatus, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator);

        var result = await sut.DeleteAsync(tenant.TenantId, reason: "Account closure");

        Assert.True(result.Success);
        Assert.Equal(previousStatus, result.PreviousStatus);
        Assert.Equal(TenantStatus.Deleted, result.NewStatus);
        Assert.Equal(1, mediator.PublishCalls);
        var published = Assert.Single(mediator.Published);
        Assert.Equal(previousStatus, published.PreviousStatus);
        Assert.Equal(TenantStatus.Deleted, published.NewStatus);
    }

    // --- Store-write failure handling ------------------------------------------------

    [Fact]
    public async Task Service_returns_Success_false_with_FailureReason_when_store_update_throws()
    {
        var tenant = NewTenant(status: TenantStatus.Active, emailVerified: true);
        var thrown = new InvalidOperationException("store boom");
        var store = new InMemoryStore(tenant) { ThrowFromUpdate = thrown };
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator);

        var result = await sut.DeactivateAsync(tenant.TenantId, reason: "Will throw");

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("store boom", result.FailureReason!);
        Assert.Equal(TenantStatus.Active, result.PreviousStatus);
        Assert.Equal(TenantStatus.Active, result.NewStatus);
        // In-memory state was reverted on the tracked tenant entity.
        Assert.Equal(TenantStatus.Active, tenant.Status);
        // No notification fired.
        Assert.Equal(0, mediator.PublishCalls);
    }

    [Fact]
    public async Task Service_returns_Success_true_when_store_update_succeeds_but_MediatR_publish_throws()
    {
        var tenant = NewTenant(status: TenantStatus.Active, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator
        {
            ThrowFromPublish = new InvalidOperationException("downstream handler exploded"),
        };
        var log = new RecordingLogger<DefaultTenantStatusService>();
        var sut = NewService(store, mediator, logger: log);

        var result = await sut.DeactivateAsync(tenant.TenantId, reason: "Best-effort dispatch");

        // Per the documented contract: state change persisted; notification dispatch is
        // best-effort and logged at Error on failure but the call still reports Success.
        Assert.True(result.Success, result.FailureReason);
        Assert.Equal(TenantStatus.Inactive, result.NewStatus);
        Assert.Equal(TenantStatus.Inactive, tenant.Status);
        Assert.Equal(1, store.UpdateCalls);
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Error &&
            e.Message.Contains("notification dispatch failed", StringComparison.OrdinalIgnoreCase));
    }

    // --- Notification payload --------------------------------------------------------

    [Fact]
    public async Task Notification_payload_includes_all_required_tenant_and_lifecycle_fields()
    {
        var tenantId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var tenant = new PolarTenantInfo
        {
            Id = tenantId.ToString(),
            Identifier = "acme-corp",
            Name = "ACME Corporation",
            Status = TenantStatus.Active,
            SiteManagerEmail = "ops@acme.example",
            SiteManagerEmailVerified = true,
            SiteManagerPhone = "+15555550100",
        };
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 17, 12, 30, 45, TimeSpan.Zero));
        var sut = NewService(store, mediator, clock: clock);

        var result = await sut.SuspendAsync(tenantId, reason: "Billing review", actorUserId: actor);

        Assert.True(result.Success);
        var published = Assert.Single(mediator.Published);
        Assert.Equal(tenantId, published.TenantId);
        Assert.Equal("acme-corp", published.TenantIdentifier);
        Assert.Equal("ACME Corporation", published.TenantName);
        Assert.Equal(TenantStatus.Active, published.PreviousStatus);
        Assert.Equal(TenantStatus.Suspended, published.NewStatus);
        Assert.Equal("Billing review", published.Reason);
        Assert.Equal(actor, published.ActorUserId);
        Assert.Equal(clock.GetUtcNow(), published.OccurredAt);
        Assert.Equal("ops@acme.example", published.SiteManagerEmail);
        Assert.True(published.SiteManagerEmailVerified);
        Assert.Equal("+15555550100", published.SiteManagerPhone);
    }

    [Fact]
    public async Task OccurredAt_uses_injected_TimeProvider()
    {
        var stamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var clock = new FixedTimeProvider(stamp);
        var tenant = NewTenant(status: TenantStatus.Active, emailVerified: true);
        var store = new InMemoryStore(tenant);
        var mediator = new RecordingMediator();
        var sut = NewService(store, mediator, clock: clock);

        var result = await sut.DeactivateAsync(tenant.TenantId, reason: "Clock test");

        Assert.True(result.Success);
        Assert.Equal(stamp, result.OccurredAt);
        var published = Assert.Single(mediator.Published);
        Assert.Equal(stamp, published.OccurredAt);
    }

    // --- helpers ---------------------------------------------------------------------

    private static PolarTenantInfo NewTenant(TenantStatus status, bool emailVerified)
    {
        return new PolarTenantInfo
        {
            Id = Guid.NewGuid().ToString(),
            Identifier = "test-tenant",
            Name = "Test Tenant",
            Status = status,
            SiteManagerEmail = "owner@test.example",
            SiteManagerEmailVerified = emailVerified,
            SiteManagerPhone = null,
        };
    }

    private static DefaultTenantStatusService NewService(
        InMemoryStore store,
        RecordingMediator mediator,
        TenantStatusServiceOptions? options = null,
        TimeProvider? clock = null,
        ILogger<DefaultTenantStatusService>? logger = null)
    {
        return new DefaultTenantStatusService(
            store,
            mediator,
            logger ?? NullLogger<DefaultTenantStatusService>.Instance,
            new StaticOptionsMonitor<TenantStatusServiceOptions>(options ?? new TenantStatusServiceOptions()),
            clock ?? TimeProvider.System);
    }

    // --- Test doubles ----------------------------------------------------------------

    /// <summary>
    /// In-memory <see cref="IMultiTenantStore{TTenantInfo}"/> stub. Mutating
    /// <see cref="UpdateAsync"/> records every call. When <see cref="ThrowFromUpdate"/> is
    /// set, the update throws after recording the attempt — used to exercise the SUT's
    /// store-failure revert path.
    /// </summary>
    private sealed class InMemoryStore : IMultiTenantStore<PolarTenantInfo>
    {
        private readonly List<PolarTenantInfo> _tenants;

        public InMemoryStore(params PolarTenantInfo[] seed)
        {
            _tenants = new List<PolarTenantInfo>(seed);
        }

        public int UpdateCalls { get; private set; }
        public Exception? ThrowFromUpdate { get; set; }
        public bool ReturnFalseFromUpdate { get; set; }

        public Task<bool> TryAddAsync(PolarTenantInfo tenantInfo) => AddAsync(tenantInfo);
        public Task<bool> AddAsync(PolarTenantInfo tenantInfo)
        {
            if (_tenants.Any(t => t.Identifier == tenantInfo.Identifier)) return Task.FromResult(false);
            _tenants.Add(tenantInfo);
            return Task.FromResult(true);
        }
        public Task<bool> TryUpdateAsync(PolarTenantInfo tenantInfo) => UpdateAsync(tenantInfo);
        public Task<bool> UpdateAsync(PolarTenantInfo tenantInfo)
        {
            UpdateCalls++;
            if (ThrowFromUpdate is not null)
            {
                throw ThrowFromUpdate;
            }
            if (ReturnFalseFromUpdate)
            {
                return Task.FromResult(false);
            }
            // The SUT mutates the tenant reference directly; the in-memory list already
            // points at the same reference so there's nothing to overwrite.
            return Task.FromResult(true);
        }
        public Task<bool> TryRemoveAsync(string identifier) => RemoveAsync(identifier);
        public Task<bool> RemoveAsync(string identifier) =>
            Task.FromResult(_tenants.RemoveAll(t => t.Identifier == identifier) > 0);
        public Task<PolarTenantInfo?> TryGetAsync(string id) =>
            Task.FromResult(_tenants.FirstOrDefault(t => t.Id == id));
        public Task<PolarTenantInfo?> GetAsync(string id) => TryGetAsync(id);
        public Task<PolarTenantInfo?> TryGetByIdentifierAsync(string identifier) =>
            Task.FromResult(_tenants.FirstOrDefault(t => t.Identifier == identifier));
        public Task<PolarTenantInfo?> GetByIdentifierAsync(string identifier) => TryGetByIdentifierAsync(identifier);
        public Task<IEnumerable<PolarTenantInfo>> GetAllAsync() =>
            Task.FromResult<IEnumerable<PolarTenantInfo>>(_tenants.ToArray());
        public Task<IEnumerable<PolarTenantInfo>> GetAllAsync(int take, int skip) =>
            Task.FromResult<IEnumerable<PolarTenantInfo>>(_tenants.Skip(skip).Take(take).ToArray());
    }

    /// <summary>
    /// <see cref="IMediator"/> stub that records published notifications and optionally
    /// throws from <see cref="Publish(object, CancellationToken)"/> / its generic siblings to
    /// exercise the SUT's best-effort dispatch contract.
    /// </summary>
    private sealed class RecordingMediator : IMediator
    {
        public List<TenantStatusChangedNotification> Published { get; } = new();
        public int PublishCalls { get; private set; }
        public Exception? ThrowFromPublish { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            PublishCalls++;
            if (notification is TenantStatusChangedNotification typed)
            {
                Published.Add(typed);
            }
            if (ThrowFromPublish is not null) throw ThrowFromPublish;
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            PublishCalls++;
            if (notification is TenantStatusChangedNotification typed)
            {
                Published.Add(typed);
            }
            if (ThrowFromPublish is not null) throw ThrowFromPublish;
            return Task.CompletedTask;
        }

        // --- IMediator surface we don't exercise; throw so any accidental call is loud ---

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("RecordingMediator does not implement Send.");

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("RecordingMediator does not implement Send.");

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw new NotSupportedException("RecordingMediator does not implement Send.");

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("RecordingMediator does not implement CreateStream.");

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("RecordingMediator does not implement CreateStream.");
    }

    /// <summary>
    /// Minimal <see cref="IOptionsMonitor{TOptions}"/> stub returning a fixed instance. The
    /// SUT only ever reads <see cref="IOptionsMonitor{TOptions}.CurrentValue"/>; OnChange
    /// callbacks are never invoked.
    /// </summary>
    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T current) { CurrentValue = current; }
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    /// <summary>
    /// Minimal <see cref="TimeProvider"/> stub returning a fixed instant for every call to
    /// <see cref="GetUtcNow"/>. Avoids taking a dependency on
    /// <c>Microsoft.Extensions.TimeProvider.Testing</c> for the small handful of clock
    /// assertions we need.
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>
    /// <see cref="ILogger{T}"/> stub that captures every log call. Used to assert
    /// failure-path logging behaviour without resorting to a snapshot framework.
    /// </summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
