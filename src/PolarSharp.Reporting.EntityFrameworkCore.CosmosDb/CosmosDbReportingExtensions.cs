using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant;
using PolarSharp.Reporting;
using PolarSharp.Reporting.EntityFrameworkCore;

namespace PolarSharp.Reporting.EntityFrameworkCore.CosmosDb;

/// <summary>
/// Azure Cosmos DB provider for the Reporting snapshot DbContext.
/// </summary>
/// <remarks>
/// <para>
/// Reporting snapshot entities partition on TenantId; the snapshot service writes per-tenant
/// to single partitions (efficient); cross-tenant aggregations (used by SaaSAdmin reports)
/// are rejected on Cosmos because they would require cross-partition queries with high RU
/// cost. SaaSAdmin cross-tenant reporting needs an alternate provider (Postgres / SqlServer)
/// when those reports matter to the host.
/// </para>
/// <para>
/// Hierarchical drilldown (Customer → Orders → LineItems) requires snapshot mode ENABLED
/// with aggressive pre-aggregation, because EF Core's Cosmos provider does NOT translate
/// JOIN operations. The drilldown reads pre-aggregated parent records (customer with
/// order_count + lifetime_value) + per-customer single-partition queries for the order list.
/// </para>
/// </remarks>
public static class CosmosDbReportingExtensions
{
    /// <summary>Registers a Cosmos DB-backed <see cref="PolarReportingDbContext"/> + <see cref="IPolarReportingClient"/>.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="accountEndpoint">Cosmos account endpoint URL.</param>
    /// <param name="accountKey">Cosmos account master key.</param>
    /// <param name="databaseName">Cosmos database name.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseCosmosDbReporting(this IServiceCollection services, string accountEndpoint, string accountKey, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(accountEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(accountKey);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);

        services.AddDbContext<PolarReportingDbContext>(opts =>
            opts.UseCosmos(accountEndpoint, accountKey, databaseName));
        services.AddHealthChecks()
            .AddDbContextCheck<PolarReportingDbContext>(name: "polar-reporting-sql", tags: ["polar-sql", "polar-reporting", "polar-cosmosdb"]);
        services.AddScoped<IPolarReportingClient, EfPolarReportingClient>();
        return services;
    }

    /// <summary>Registers a Cosmos DB-backed Reporting DbContext using a connection string.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="connectionString">Cosmos connection string.</param>
    /// <param name="databaseName">Cosmos database name.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseCosmosDbReporting(this IServiceCollection services, string connectionString, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);

        services.AddDbContext<PolarReportingDbContext>(opts =>
            opts.UseCosmos(connectionString, databaseName));
        services.AddHealthChecks()
            .AddDbContextCheck<PolarReportingDbContext>(name: "polar-reporting-sql", tags: ["polar-sql", "polar-reporting", "polar-cosmosdb"]);
        services.AddScoped<IPolarReportingClient, EfPolarReportingClient>();
        return services;
    }
}

/// <summary>Design-time factory for the Reporting DbContext (Cosmos DB).</summary>
public sealed class PolarReportingDbContextCosmosDbDesignTimeFactory : IDesignTimeDbContextFactory<PolarReportingDbContext>
{
    /// <inheritdoc/>
    public PolarReportingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarReportingDbContext>()
            .UseCosmos(
                accountEndpoint: "https://localhost:8081",
                accountKey: "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
                databaseName: "polar_reporting_design")
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
