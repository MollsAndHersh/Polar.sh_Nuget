using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Tests.Upgrade;

/// <summary>
/// Tests for <see cref="SingleTenantUpgradeHostedService"/> — the orchestrator that wires the
/// configured <see cref="DefaultTenantStrategy"/>, the per-provider migrator, and the tenant
/// registry upgrader together at host startup.
/// </summary>
public sealed class SingleTenantUpgradeHostedServiceTests
{
    // --- Early-exit cases ---------------------------------------------------------------

    [Fact]
    public async Task StartAsync_bails_early_when_EnableAutomaticUpgrade_is_false()
    {
        var migrator = new StubMigrator();
        var log = new RecordingLogger<SingleTenantUpgradeHostedService>();
        var sp = BuildServiceProvider(migrator, configure: opts => opts.EnableAutomaticUpgrade = false);

        var sut = new SingleTenantUpgradeHostedService(
            sp, Options.Create(GetOptions(sp)), log);

        await sut.StartAsync(CancellationToken.None);

        Assert.Equal(0, migrator.RunCalls);
        Assert.Equal(0, migrator.HasCompletedCalls);
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("automatic upgrade disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartAsync_bails_early_when_migrator_HasUpgradeCompletedAsync_returns_true()
    {
        var migrator = new StubMigrator { HasCompleted = true };
        var log = new RecordingLogger<SingleTenantUpgradeHostedService>();
        var sp = BuildServiceProvider(migrator);

        var sut = new SingleTenantUpgradeHostedService(
            sp, Options.Create(GetOptions(sp)), log);

        await sut.StartAsync(CancellationToken.None);

        Assert.Equal(1, migrator.HasCompletedCalls);
        Assert.Equal(0, migrator.RunCalls);
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Information &&
            (e.Message.Contains("already complete", StringComparison.OrdinalIgnoreCase)
             || e.Message.Contains("completion marker present", StringComparison.OrdinalIgnoreCase)));
    }

    // --- LiteralDefault strategy --------------------------------------------------------

    [Fact]
    public async Task StartAsync_resolves_LiteralDefault_tenant_and_invokes_migrator()
    {
        var migrator = new StubMigrator();
        var sp = BuildServiceProvider(migrator, configure: opts =>
        {
            opts.DefaultTenantStrategy = DefaultTenantStrategy.LiteralDefault;
            opts.LiteralDefaultTenantSlug = "anchor";
            opts.LiteralDefaultTenantName = "Anchor Tenant";
        });

        var sut = new SingleTenantUpgradeHostedService(
            sp, Options.Create(GetOptions(sp)), NullLogger<SingleTenantUpgradeHostedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        Assert.Equal(1, migrator.RunCalls);
        Assert.NotNull(migrator.LastTenant);
        Assert.Equal("anchor", migrator.LastTenant!.Identifier);
        Assert.Equal("Anchor Tenant", migrator.LastTenant.Name);
        Assert.False(string.IsNullOrEmpty(migrator.LastTenant.Id));
        Assert.True(Guid.TryParse(migrator.LastTenant.Id, out _),
            "Tenant Id should be a parseable GUID string.");
    }

    // --- HostSupplied strategy ----------------------------------------------------------

    [Fact]
    public async Task StartAsync_uses_IDefaultTenantResolver_when_strategy_is_HostSupplied()
    {
        var configured = new PolarTenantInfo
        {
            Id = Guid.NewGuid().ToString(),
            Identifier = "host-supplied",
            Name = "Host Supplied Tenant",
        };
        var resolver = new StubResolver(configured);
        var migrator = new StubMigrator();

        var sp = BuildServiceProvider(
            migrator,
            configure: opts => opts.DefaultTenantStrategy = DefaultTenantStrategy.HostSupplied,
            extraRegistrations: s => s.AddScoped<IDefaultTenantResolver>(_ => resolver));

        var sut = new SingleTenantUpgradeHostedService(
            sp, Options.Create(GetOptions(sp)), NullLogger<SingleTenantUpgradeHostedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        Assert.Equal(1, resolver.ResolveCalls);
        Assert.Equal(1, migrator.RunCalls);
        Assert.NotNull(migrator.LastTenant);
        Assert.Equal(configured.Id, migrator.LastTenant!.Id);
        Assert.Equal("host-supplied", migrator.LastTenant.Identifier);
    }

    [Fact]
    public async Task StartAsync_throws_when_HostSupplied_strategy_used_without_IDefaultTenantResolver_registered()
    {
        var migrator = new StubMigrator();
        var sp = BuildServiceProvider(migrator,
            configure: opts => opts.DefaultTenantStrategy = DefaultTenantStrategy.HostSupplied);
            // Deliberately NOT registering IDefaultTenantResolver.

        var sut = new SingleTenantUpgradeHostedService(
            sp, Options.Create(GetOptions(sp)), NullLogger<SingleTenantUpgradeHostedService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.StartAsync(CancellationToken.None));
        Assert.Contains("IDefaultTenantResolver", ex.Message);
        Assert.Contains("HostSupplied", ex.Message);
        Assert.Equal(0, migrator.RunCalls);
    }

    // --- FirstUserOrganization strategy (not implemented in Stage A) --------------------

    [Fact]
    public async Task StartAsync_throws_NotSupportedException_for_FirstUserOrganization_strategy()
    {
        var migrator = new StubMigrator();
        var sp = BuildServiceProvider(migrator,
            configure: opts => opts.DefaultTenantStrategy = DefaultTenantStrategy.FirstUserOrganization);

        var sut = new SingleTenantUpgradeHostedService(
            sp, Options.Create(GetOptions(sp)), NullLogger<SingleTenantUpgradeHostedService>.Instance);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => sut.StartAsync(CancellationToken.None));
        Assert.Contains("PolarSharp.MultiTenant.Identity", ex.Message);
        Assert.Contains("Stage A", ex.Message);
        Assert.Equal(0, migrator.RunCalls);
    }

    // --- Migrator failure handling ------------------------------------------------------

    [Fact]
    public async Task StartAsync_logs_Error_and_throws_when_migrator_returns_Success_equals_false()
    {
        var migrator = new StubMigrator
        {
            // Migrator returns a failed result rather than throwing.
            ConfiguredResult = new SingleTenantUpgradeResult
            {
                Success = false,
                AlreadyComplete = false,
                RowsStamped = 0,
                RowsStampedByEntityType = new Dictionary<string, long>(),
                Duration = TimeSpan.FromSeconds(2),
                Message = "synthetic-failure",
            },
        };
        var log = new RecordingLogger<SingleTenantUpgradeHostedService>();
        var sp = BuildServiceProvider(migrator);

        var sut = new SingleTenantUpgradeHostedService(
            sp, Options.Create(GetOptions(sp)), log);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.StartAsync(CancellationToken.None));
        Assert.Contains("synthetic-failure", ex.Message);
        Assert.Equal(1, migrator.RunCalls);
    }

    [Fact]
    public async Task StartAsync_logs_Error_and_rethrows_when_migrator_throws()
    {
        var thrown = new InvalidOperationException("boom from migrator");
        var migrator = new StubMigrator { ThrowFromRun = thrown };
        var log = new RecordingLogger<SingleTenantUpgradeHostedService>();
        var sp = BuildServiceProvider(migrator);

        var sut = new SingleTenantUpgradeHostedService(
            sp, Options.Create(GetOptions(sp)), log);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.StartAsync(CancellationToken.None));
        Assert.Same(thrown, ex);

        // Error should have been logged before rethrowing.
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Error &&
            e.Message.Contains("failed", StringComparison.OrdinalIgnoreCase));
    }

    // --- MaxRunDuration cancellation ----------------------------------------------------

    [Fact]
    public async Task StartAsync_respects_MaxRunDuration_via_linked_cancellation_token()
    {
        // The orchestrator wires CancelAfter(opts.MaxRunDuration) onto a linked CTS and
        // passes the resulting token to the migrator. The stub awaits Task.Delay(long)
        // on the supplied token, so cancellation should manifest as an OperationCanceledException.
        var migrator = new StubMigrator
        {
            DelayOnRun = TimeSpan.FromSeconds(5),
        };
        var sp = BuildServiceProvider(migrator,
            configure: opts => opts.MaxRunDuration = TimeSpan.FromMilliseconds(50));

        var sut = new SingleTenantUpgradeHostedService(
            sp, Options.Create(GetOptions(sp)), NullLogger<SingleTenantUpgradeHostedService>.Instance);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => sut.StartAsync(CancellationToken.None));
        // Migrator started but was canceled before completing.
        Assert.Equal(1, migrator.RunCalls);
        Assert.True(migrator.SuppliedTokenWasCanceled,
            "Migrator's supplied token should have been canceled by the MaxRunDuration ceiling.");
    }

    // --- StopAsync ----------------------------------------------------------------------

    [Fact]
    public async Task StopAsync_is_a_noop()
    {
        var migrator = new StubMigrator();
        var sp = BuildServiceProvider(migrator);
        var sut = new SingleTenantUpgradeHostedService(
            sp, Options.Create(GetOptions(sp)), NullLogger<SingleTenantUpgradeHostedService>.Instance);

        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(0, migrator.RunCalls);
        Assert.Equal(0, migrator.HasCompletedCalls);
    }

    // --- DI harness ---------------------------------------------------------------------

    private static ServiceProvider BuildServiceProvider(
        StubMigrator migrator,
        Action<SingleTenantUpgradeOptions>? configure = null,
        Action<IServiceCollection>? extraRegistrations = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<ISingleTenantUpgradeMigrator>(migrator);

        var store = new InMemoryRegistryStore();
        services.AddSingleton<IMultiTenantStore<PolarTenantInfo>>(store);
        services.AddScoped<ITenantRegistryUpgrader, DefaultTenantRegistryUpgrader>();

        var opts = new SingleTenantUpgradeOptions();
        configure?.Invoke(opts);
        services.AddSingleton(Options.Create(opts));
        // Also register the options instance directly so tests can pull it back via DI.
        services.AddSingleton(opts);

        extraRegistrations?.Invoke(services);

        return services.BuildServiceProvider();
    }

    private static SingleTenantUpgradeOptions GetOptions(IServiceProvider sp) =>
        sp.GetRequiredService<SingleTenantUpgradeOptions>();

    // --- Test doubles -------------------------------------------------------------------

    private sealed class StubMigrator : ISingleTenantUpgradeMigrator
    {
        public bool HasCompleted { get; set; }
        public int HasCompletedCalls { get; private set; }
        public int RunCalls { get; private set; }
        public PolarTenantInfo? LastTenant { get; private set; }
        public Exception? ThrowFromRun { get; set; }
        public TimeSpan DelayOnRun { get; set; } = TimeSpan.Zero;
        public bool SuppliedTokenWasCanceled { get; private set; }
        public SingleTenantUpgradeResult? ConfiguredResult { get; set; }

        public Task<bool> HasUpgradeCompletedAsync(CancellationToken ct)
        {
            HasCompletedCalls++;
            return Task.FromResult(HasCompleted);
        }

        public async Task<SingleTenantUpgradeResult> RunAsync(PolarTenantInfo defaultTenant, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(defaultTenant);
            RunCalls++;
            LastTenant = defaultTenant;

            if (DelayOnRun > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(DelayOnRun, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    SuppliedTokenWasCanceled = true;
                    throw;
                }
            }

            if (ThrowFromRun is not null)
            {
                throw ThrowFromRun;
            }

            return ConfiguredResult ?? new SingleTenantUpgradeResult
            {
                Success = true,
                AlreadyComplete = false,
                RowsStamped = 0,
                RowsStampedByEntityType = new Dictionary<string, long>(),
                Duration = TimeSpan.Zero,
            };
        }
    }

    private sealed class StubResolver : IDefaultTenantResolver
    {
        private readonly PolarTenantInfo _tenant;
        public int ResolveCalls { get; private set; }
        public StubResolver(PolarTenantInfo tenant) { _tenant = tenant; }
        public Task<PolarTenantInfo> ResolveAsync(CancellationToken ct)
        {
            ResolveCalls++;
            return Task.FromResult(_tenant);
        }
    }

    /// <summary>
    /// Minimal Finbuckle store stub. Mirrors the shape the registry upgrader expects.
    /// </summary>
    private sealed class InMemoryRegistryStore : IMultiTenantStore<PolarTenantInfo>
    {
        private readonly List<PolarTenantInfo> _tenants = new();

        public Task<bool> TryAddAsync(PolarTenantInfo tenantInfo) => AddAsync(tenantInfo);
        public Task<bool> AddAsync(PolarTenantInfo tenantInfo)
        {
            if (_tenants.Any(t => t.Identifier == tenantInfo.Identifier)) return Task.FromResult(false);
            _tenants.Add(tenantInfo);
            return Task.FromResult(true);
        }
        public Task<bool> TryUpdateAsync(PolarTenantInfo tenantInfo) => UpdateAsync(tenantInfo);
        public Task<bool> UpdateAsync(PolarTenantInfo tenantInfo) => Task.FromResult(true);
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
