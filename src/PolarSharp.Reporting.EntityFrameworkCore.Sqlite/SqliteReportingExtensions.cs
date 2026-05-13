using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant;
using PolarSharp.Reporting;
using PolarSharp.Reporting.EntityFrameworkCore;

namespace PolarSharp.Reporting.EntityFrameworkCore.Sqlite;

/// <summary>SQLite provider for the Reporting snapshot DbContext.</summary>
public static class SqliteReportingExtensions
{
    /// <summary>Registers a SQLite-backed <see cref="PolarReportingDbContext"/> + <see cref="IPolarReportingClient"/>.</summary>
    public static IServiceCollection UseSqliteReporting(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        services.AddDbContext<PolarReportingDbContext>(opts =>
            opts.UseSqlite(connectionString, sql =>
                sql.MigrationsAssembly(typeof(SqliteReportingExtensions).Assembly.GetName().Name)));
        services.AddHealthChecks()
            .AddDbContextCheck<PolarReportingDbContext>(name: "polar-reporting-sql", tags: ["polar-sql", "polar-reporting"]);
        services.AddScoped<IPolarReportingClient, EfPolarReportingClient>();
        return services;
    }
}

/// <summary>Design-time factory for <c>dotnet ef migrations add</c>.</summary>
public sealed class PolarReportingDbContextSqliteDesignTimeFactory : IDesignTimeDbContextFactory<PolarReportingDbContext>
{
    /// <inheritdoc/>
    public PolarReportingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarReportingDbContext>()
            .UseSqlite("Data Source=design-time.db",
                b => b.MigrationsAssembly(typeof(PolarReportingDbContextSqliteDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new PolarReportingDbContext(options, DesignTimeServices.Build());
    }
}

internal static class DesignTimeServices
{
    public static IServiceProvider Build() =>
        new ServiceCollection()
            .AddSingleton<IMultiTenantContextAccessor>(new StubAccessor())
            .BuildServiceProvider();

    private sealed class StubAccessor : IMultiTenantContextAccessor
    {
        private static readonly PolarTenantInfo Tenant = new() { Id = "design-time-tenant", Identifier = "design-time-tenant", Name = "Design" };
        private readonly StubContext _ctx = new(Tenant);
        public IMultiTenantContext MultiTenantContext { get => _ctx; set { /* read-only */ } }
    }

    private sealed class StubContext : IMultiTenantContext
    {
        public StubContext(PolarTenantInfo t) { TenantInfo = t; }
        public ITenantInfo? TenantInfo { get; init; }
        public StrategyInfo? StrategyInfo { get; init; }
        public object? StoreInfo { get; init; }
        public bool IsResolved => true;
    }
}
