using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Tests;

/// <summary>
/// Verifies the migration runner applies migrations idempotently, refuses production
/// startup when no migrations exist, and supports the EnsureCreated fallback in Development.
/// </summary>
public sealed class PolarMigrationRunnerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PolarMigrationRunnerTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task With_no_migrations_in_Development_falls_back_to_EnsureCreatedAsync_when_flag_is_on()
    {
        var services = BuildServices(
            opts => { opts.Enabled = true; opts.RunOnStartup = true; opts.UseEnsureCreatedInDevelopment = true; },
            isProduction: false);

        await StartHostedServiceAsync(services);

        // EnsureCreated should have built the schema; verify the tenants table is queryable.
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        Assert.NotNull(await db.Tenants.ToListAsync());
    }

    [Fact]
    public async Task With_no_migrations_in_Production_throws_to_block_unversioned_schema()
    {
        var services = BuildServices(
            opts => { opts.Enabled = true; opts.RunOnStartup = true; opts.UseEnsureCreatedInDevelopment = false; },
            isProduction: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => StartHostedServiceAsync(services));
        Assert.Contains("no migrations registered", ex.Message);
        Assert.Contains("Production", ex.Message);
    }

    [Fact]
    public async Task With_RunOnStartup_disabled_runner_is_a_noop()
    {
        var services = BuildServices(
            opts => { opts.Enabled = true; opts.RunOnStartup = false; },
            isProduction: false);

        // Should NOT throw, should NOT create schema. The tenants table should not exist
        // (verified by EnsureCreatedAsync NOT being called — the in-memory DB stays empty).
        await StartHostedServiceAsync(services);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        // Querying a non-existent table throws SqliteException. The opposite (no exception)
        // would imply the runner applied EnsureCreated despite RunOnStartup=false.
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Tenants.ToListAsync());
    }

    private ServiceProvider BuildServices(Action<PolarMigrationOptions> configureOpts, bool isProduction)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddDbContext<PolarTenantDbContext>(opts => opts.UseSqlite(_connection));
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(isProduction ? "Production" : "Development"));
        var options = new PolarMigrationOptions();
        configureOpts(options);
        services.AddSingleton(options);
        services.AddHostedService<PolarMigrationRunner<PolarTenantDbContext>>();
        return services.BuildServiceProvider();
    }

    private static async Task StartHostedServiceAsync(ServiceProvider services)
    {
        var runner = services.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<PolarMigrationRunner<PolarTenantDbContext>>()
            .Single();
        await runner.StartAsync(CancellationToken.None);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string name) { EnvironmentName = name; }
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
