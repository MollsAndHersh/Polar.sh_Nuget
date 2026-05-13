using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant;
using PolarSharp.Reporting.EntityFrameworkCore;

namespace PolarSharp.Reporting.Tests.Infrastructure;

/// <summary>
/// Test harness spinning up a real <see cref="PolarReportingDbContext"/> over in-memory
/// SQLite with a faked Finbuckle <see cref="IMultiTenantContextAccessor"/>. Mirrors the
/// catalog-side <c>CatalogTestContext</c> pattern so reporting EF-backed services can be
/// tested at the same fidelity.
/// </summary>
public sealed class ReportingTestContext : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MutableMultiTenantAccessor _accessor;
    private readonly ServiceProvider _services;

    public const string DefaultTenantId = "tenant-test";

    private ReportingTestContext(SqliteConnection connection, MutableMultiTenantAccessor accessor, ServiceProvider services)
    {
        _connection = connection;
        _accessor = accessor;
        _services = services;
    }

    public IServiceProvider Services => _services;

    public string CurrentTenantId => _accessor.MultiTenantContext.TenantInfo?.Id
        ?? throw new InvalidOperationException("No tenant currently in scope.");

    public void SetCurrentTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        _accessor.SwitchTo(tenantId);
    }

    public IServiceScope CreateScope() => _services.CreateScope();

    public static async Task<ReportingTestContext> CreateAsync(
        string initialTenantId = DefaultTenantId,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(initialTenantId);

        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var accessor = new MutableMultiTenantAccessor(initialTenantId);

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IMultiTenantContextAccessor>(accessor);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDbContext<PolarReportingDbContext>(opts => opts.UseSqlite(connection));

        configureServices?.Invoke(services);

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        return new ReportingTestContext(connection, accessor, provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        _connection.Dispose();
    }

    private sealed class MutableMultiTenantAccessor : IMultiTenantContextAccessor
    {
        private IMultiTenantContext _current;

        public MutableMultiTenantAccessor(string tenantId)
        {
            _current = BuildContext(tenantId);
        }

        public IMultiTenantContext MultiTenantContext
        {
            get => _current;
            set => _current = value;
        }

        public void SwitchTo(string tenantId) => _current = BuildContext(tenantId);

        private static IMultiTenantContext BuildContext(string tenantId) =>
            new MultiTenantContext<PolarTenantInfo>(
                new PolarTenantInfo { Id = tenantId, Identifier = tenantId, Name = tenantId });
    }
}
