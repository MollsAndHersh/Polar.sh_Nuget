using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant;
using PolarSharp.Reporting;
using PolarSharp.Reporting.EntityFrameworkCore;

namespace PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL;

/// <summary>PostgreSQL provider for the Reporting snapshot DbContext.</summary>
public static class PostgreSqlReportingExtensions
{
    /// <summary>Registers a PostgreSQL-backed <see cref="PolarReportingDbContext"/> + <see cref="IPolarReportingClient"/>.</summary>
    public static IServiceCollection UsePostgreSqlReporting(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        // V20-008 Layer 2.
        services.AddScoped<global::PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL.PostgreSqlTenantSessionInterceptor>();

        services.AddDbContext<PolarReportingDbContext>((sp, opts) =>
            opts.UseNpgsql(connectionString, npg =>
                    npg.MigrationsAssembly(typeof(PostgreSqlReportingExtensions).Assembly.GetName().Name))
                .AddInterceptors(sp.GetRequiredService<global::PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL.PostgreSqlTenantSessionInterceptor>()));
        services.AddHealthChecks()
            .AddDbContextCheck<PolarReportingDbContext>(name: "polar-reporting-sql", tags: ["polar-sql", "polar-reporting"]);
        services.AddScoped<IPolarReportingClient, EfPolarReportingClient>();
        return services;
    }
}

/// <summary>Design-time factory for <c>dotnet ef migrations add</c>.</summary>
public sealed class PolarReportingDbContextPostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<PolarReportingDbContext>
{
    /// <inheritdoc/>
    public PolarReportingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarReportingDbContext>()
            .UseNpgsql("Host=localhost;Database=polar_reporting_design;Username=postgres",
                b => b.MigrationsAssembly(typeof(PolarReportingDbContextPostgreSqlDesignTimeFactory).Assembly.GetName().Name))
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
