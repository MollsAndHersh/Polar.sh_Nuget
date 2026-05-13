using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;

/// <summary>
/// Test harness that spins up a real <see cref="PolarCatalogDbContext"/> backed by an
/// in-memory SQLite connection, with a stub Finbuckle <see cref="IMultiTenantContextAccessor"/>
/// returning a configurable current tenant. Reusable across cloning, translation repo,
/// catalog reader, publisher, and any other EF-backed service test.
/// </summary>
/// <remarks>
/// <para>
/// The harness opens the SQLite connection eagerly (so the in-memory database persists for
/// the lifetime of the harness — closing the connection drops the DB), builds a service
/// provider with the DbContext + faked tenant accessor + logger, then calls
/// <c>EnsureCreatedAsync</c> so the schema is in place before tests run.
/// </para>
/// <para>
/// Per-test tenant switching is supported via <see cref="SetCurrentTenant"/>; cross-tenant
/// scenarios drive each tenant's slice via the same DbContext instance, exercising the
/// global query filter the way production code would.
/// </para>
/// </remarks>
public sealed class CatalogTestContext : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MutableMultiTenantAccessor _accessor;
    private readonly ServiceProvider _services;

    /// <summary>The default tenant id used when no explicit override is supplied.</summary>
    public const string DefaultTenantId = "tenant-test";

    private CatalogTestContext(SqliteConnection connection, MutableMultiTenantAccessor accessor, ServiceProvider services)
    {
        _connection = connection;
        _accessor = accessor;
        _services = services;
    }

    /// <summary>The root <see cref="IServiceProvider"/>. Tests resolve services from this.</summary>
    public IServiceProvider Services => _services;

    /// <summary>The current tenant id in scope. Reads compose the global filter through the DbContext.</summary>
    public string CurrentTenantId => _accessor.MultiTenantContext.TenantInfo?.Id
        ?? throw new InvalidOperationException("No tenant currently in scope.");

    /// <summary>Switches the in-scope tenant. Subsequent DbContext resolutions see the new tenant.</summary>
    public void SetCurrentTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        _accessor.SwitchTo(tenantId);
    }

    /// <summary>Sets the Polar organization id on the current tenant info — used by services that need it (e.g. <c>LicenseKeyValidator</c>).</summary>
    public void SetPolarOrganizationId(string polarOrganizationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(polarOrganizationId);
        _accessor.SetPolarOrganizationId(polarOrganizationId);
    }

    /// <summary>Creates a DI scope. Each test typically opens one scope to resolve the DbContext + services together.</summary>
    public IServiceScope CreateScope() => _services.CreateScope();

    /// <summary>
    /// Convenience: resolves <see cref="PolarCatalogDbContext"/> directly out of the root
    /// provider for one-off seed / inspection operations. For service-under-test calls,
    /// prefer <see cref="CreateScope"/> so DbContext lifetimes match production.
    /// </summary>
    public PolarCatalogDbContext NewDbContext()
    {
        var scope = _services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
    }

    /// <summary>
    /// Builds and initialises the harness. Caller is responsible for disposal.
    /// </summary>
    /// <param name="initialTenantId">Initial tenant id placed in scope.</param>
    /// <param name="configureServices">Optional callback to register additional services (e.g. cloning, reader, translation repo) on top of the harness's base DI graph.</param>
    public static async Task<CatalogTestContext> CreateAsync(
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
        // Mirror production wiring: pull AuditLogSaveChangesInterceptor (and any other
        // ISaveChangesInterceptor) out of DI when present. Tests that exercise the interceptor
        // register it via configureServices; other tests get the default behaviour with no
        // interceptor wired (and zero perf cost).
        services.AddDbContext<PolarCatalogDbContext>((sp, opts) =>
        {
            opts.UseSqlite(connection);
            var auditInterceptor = sp.GetService<AuditLogSaveChangesInterceptor>();
            if (auditInterceptor is not null) opts.AddInterceptors(auditInterceptor);
        });

        configureServices?.Invoke(services);

        var provider = services.BuildServiceProvider();

        // Build schema once for the harness lifetime.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        return new CatalogTestContext(connection, accessor, provider);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        _connection.Dispose();
    }

    /// <summary>
    /// Mutable <see cref="IMultiTenantContextAccessor"/> impl so tests can switch the active
    /// tenant mid-stream to exercise cross-tenant isolation.
    /// </summary>
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

        public void SetPolarOrganizationId(string polarOrganizationId)
        {
            if (_current.TenantInfo is PolarTenantInfo polar)
            {
                polar.PolarOrganizationId = polarOrganizationId;
            }
        }

        private static IMultiTenantContext BuildContext(string tenantId) =>
            new MultiTenantContext<PolarTenantInfo>(
                new PolarTenantInfo { Id = tenantId, Identifier = tenantId, Name = tenantId });
    }
}
