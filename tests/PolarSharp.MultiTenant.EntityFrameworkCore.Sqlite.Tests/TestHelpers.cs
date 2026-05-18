using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Tests;

/// <summary>
/// Shared test doubles + builders used across the SQLite + Litestream test suites. Kept in
/// one file so the per-suite test classes stay focused on assertions.
/// </summary>
internal static class TestHelpers
{
    /// <summary>Builds a fully-valid <see cref="LitestreamOptions"/> with the master toggle enabled and an S3 target wired up.</summary>
    /// <remarks>
    /// Tests that exercise a non-S3 target overwrite <see cref="LitestreamOptions.ReplicaTargetType"/>
    /// and the matching sub-options object before passing the instance to the SUT.
    /// </remarks>
    public static LitestreamOptions FullyEnabledOptions(int metricsPort = 9090) => new()
    {
        UseLitestream = true,
        ReplicaTargetType = LitestreamReplicaTargetType.S3,
        S3 = new LitestreamS3Options
        {
            Bucket = "polar-test-bucket",
            Region = "us-east-1",
            PathPrefix = "polarsharp/tenants/",
            AccessKeyIdEnvVar = "AWS_ACCESS_KEY_ID",
            SecretAccessKeyEnvVar = "AWS_SECRET_ACCESS_KEY",
        },
        SyncIntervalSeconds = 1,
        SnapshotIntervalMinutes = 60,
        RetentionDays = 30,
        MetricsPort = metricsPort,
        HealthCheckEnabled = true,
        HealthCheckMaxLagSeconds = 30,
        AutoRegenerateOnTenantChange = false,
    };

    /// <summary>Builds a <see cref="PolarTenantInfo"/> with sensible defaults for migrator tests.</summary>
    public static PolarTenantInfo NewTenant(
        Guid? tenantId = null,
        string identifier = "default",
        string name = "Default Tenant",
        string accessToken = "polar_oat_test")
    {
        return new PolarTenantInfo
        {
            Id = (tenantId ?? Guid.NewGuid()).ToString(),
            Identifier = identifier,
            Name = name,
            PolarAccessToken = accessToken,
            SiteManagerEmail = "ops@example.com",
        };
    }
}

/// <summary>
/// Disposable temporary-directory fixture. Each test instance gets its own scratch folder
/// outside the repo so concurrent test runs cannot collide.
/// </summary>
internal sealed class TempDirectoryFixture : IDisposable
{
    public string Path { get; }

    public TempDirectoryFixture()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "polar-sqlite-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Creates a fresh <c>master_SaaS.db</c> file in the fixture directory backed by a real
    /// <see cref="PolarTenantDbContext"/> and returns the connection string. Subsequent
    /// contexts opened against the same path observe the populated schema.
    /// </summary>
    public async Task<string> CreateMasterSaasDatabaseAsync(CancellationToken ct = default)
    {
        var masterPath = System.IO.Path.Combine(Path, SqliteBuilderExtensions.MasterSaasFileName);
        var connectionString = BuildConnectionString(masterPath);

        var options = new DbContextOptionsBuilder<PolarTenantDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var ctx = new PolarTenantDbContext(options);
        await ctx.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);
        return connectionString;
    }

    /// <summary>Touches an empty file at the given relative path inside the fixture directory.</summary>
    public string TouchFile(string relativeName, string contents = "")
    {
        var full = System.IO.Path.Combine(Path, relativeName);
        File.WriteAllText(full, contents);
        return full;
    }

    /// <summary>Builds the SQLite connection string for a master file at <paramref name="path"/>.</summary>
    /// <remarks>
    /// Mirrors <see cref="SqliteBuilderExtensions"/>'s <c>BuildMasterConnectionString</c>
    /// shape (WAL journal, shared cache) so tests exercise the same SQLite configuration as
    /// production. Cannot reference the private helper directly.
    /// </remarks>
    public static string BuildConnectionString(string masterPath)
        => $"Data Source={masterPath};Cache=Shared;Mode=ReadWriteCreate";

    /// <summary>
    /// Recursively deletes the temp directory. Best-effort: failures (e.g. file lock from a
    /// not-yet-disposed connection) are swallowed because the OS will sweep <c>%TEMP%</c>
    /// eventually and a failing cleanup is not a test failure.
    /// </summary>
    public void Dispose()
    {
        // Force GC + connection-pool drain so SQLite releases its file handles before the
        // recursive delete tries to remove the .db files.
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup — see remarks.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// Minimal <see cref="IOptionsMonitor{TOptions}"/> stub returning a fixed instance. The SUTs
/// only ever read <see cref="CurrentValue"/>; OnChange callbacks are never invoked.
/// </summary>
internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    public StaticOptionsMonitor(T current) { CurrentValue = current; }
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>
/// <see cref="ILogger{T}"/> stub capturing every log call. Used to assert
/// failure-path logging behaviour without resorting to a snapshot framework.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
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

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// <see cref="DelegatingHandler"/>-derived stub that captures the most-recent outgoing
/// <see cref="HttpRequestMessage"/> + its body (read at SendAsync time) and returns a
/// configurable response.
/// </summary>
internal sealed class CapturingHttpMessageHandler : DelegatingHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public HttpResponseMessage Response { get; set; } = new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(string.Empty),
    };
    public TimeSpan? DelayBeforeResponse { get; set; }
    public Exception? ThrowOnSend { get; set; }
    public int CallCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        if (DelayBeforeResponse is { } delay)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        if (ThrowOnSend is not null) throw ThrowOnSend;

        return Response;
    }
}

/// <summary>
/// Minimal <see cref="IHttpClientFactory"/> stub returning a single <see cref="HttpClient"/>
/// wired to the supplied <see cref="CapturingHttpMessageHandler"/>. Optionally applies a
/// timeout (used to force the LitestreamHealthCheck timeout branch).
/// </summary>
internal sealed class TestHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public TestHttpClientFactory(CapturingHttpMessageHandler handler, TimeSpan? timeout = null)
    {
        _client = new HttpClient(handler);
        if (timeout is { } t) _client.Timeout = t;
    }

    public HttpClient CreateClient(string name) => _client;
}

/// <summary>
/// Disposable scope for setting an environment variable during a test. Restores the previous
/// value on dispose so concurrent / subsequent tests are not affected.
/// </summary>
internal sealed class EnvVarScope : IDisposable
{
    private readonly string _name;
    private readonly string? _previous;

    public EnvVarScope(string name, string? value)
    {
        _name = name;
        _previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
}

/// <summary>
/// Minimal in-memory <see cref="IMultiTenantStore{TTenantInfo}"/> needed to build a real
/// <see cref="DefaultTenantRegistryUpgrader"/> against the SQLite-backed DbContext. The
/// upgrader exercises only <see cref="GetByIdentifierAsync"/> and <see cref="AddAsync"/>.
/// </summary>
/// <remarks>
/// Mirrors the Phase 1a InMemoryRegistryStore shape so tests stay portable across phases.
/// </remarks>
internal sealed class InMemoryRegistryStore : IMultiTenantStore<PolarTenantInfo>
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

/// <summary>
/// Minimal <see cref="TimeProvider"/> stub returning a fixed instant for every call to
/// <see cref="GetUtcNow"/>. Mirrors the Phase 1b FixedTimeProvider so tests stay portable.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;
    public FakeTimeProvider(DateTimeOffset now) { _now = now; }
    public override DateTimeOffset GetUtcNow() => _now;
}
