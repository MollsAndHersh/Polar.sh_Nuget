using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer;

/// <summary>Design-time factory for the catalog DbContext (SQL Server).</summary>
public sealed class PolarCatalogDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<PolarCatalogDbContext>
{
    /// <inheritdoc/>
    public PolarCatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarCatalogDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=PolarSharp.Catalog.Design;",
                b => b.MigrationsAssembly(typeof(PolarCatalogDbContextSqlServerDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        var services = DesignTimeServiceProvider.Build();
        return new PolarCatalogDbContext(options, services);
    }
}

/// <summary>
/// Builds a tiny <see cref="IServiceProvider"/> with just enough wired up for
/// <see cref="PolarCatalogDbContext"/> to construct at design time (no real tenant context).
/// </summary>
internal static class DesignTimeServiceProvider
{
    public static IServiceProvider Build() =>
        new ServiceCollection()
            .AddSingleton<IMultiTenantContextAccessor>(new DesignTimeMultiTenantContextAccessor())
            .BuildServiceProvider();

    private sealed class DesignTimeMultiTenantContextAccessor : IMultiTenantContextAccessor
    {
        private static readonly PolarTenantInfo Placeholder = new()
        {
            Id = "design-time-tenant",
            Identifier = "design-time-tenant",
            Name = "Design-time Tenant",
        };
        private readonly DesignTimeTenantContext _ctx = new(Placeholder);
        public IMultiTenantContext MultiTenantContext { get => _ctx; set { /* read-only */ } }
    }

    private sealed class DesignTimeTenantContext : IMultiTenantContext
    {
        public DesignTimeTenantContext(PolarTenantInfo tenant) { TenantInfo = tenant; }
        public ITenantInfo? TenantInfo { get; init; }
        public StrategyInfo? StrategyInfo { get; init; }
        // The non-generic IMultiTenantContext.StoreInfo type is not exported as a concrete
        // type — Finbuckle 10 uses StoreInfo<T> for the typed variant. For the design-time
        // factory we don't need a real store, so null is fine.
        public object? StoreInfo { get; init; }
        public bool IsResolved => true;
    }
}
