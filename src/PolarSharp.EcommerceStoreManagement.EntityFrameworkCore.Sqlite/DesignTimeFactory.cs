using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Sqlite;

/// <summary>Design-time factory for the catalog DbContext (SQLite).</summary>
public sealed class PolarCatalogDbContextSqliteDesignTimeFactory : IDesignTimeDbContextFactory<PolarCatalogDbContext>
{
    /// <inheritdoc/>
    public PolarCatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarCatalogDbContext>()
            .UseSqlite(
                "Data Source=design-time.db",
                b => b.MigrationsAssembly(typeof(PolarCatalogDbContextSqliteDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new PolarCatalogDbContext(options, DesignTimeServices.Build());
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
        private static readonly PolarTenantInfo Tenant = new()
        {
            Id = "design-time-tenant",
            Identifier = "design-time-tenant",
            Name = "Design-time Tenant",
        };
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
