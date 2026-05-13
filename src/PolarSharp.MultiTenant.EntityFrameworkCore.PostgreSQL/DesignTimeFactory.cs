using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL;

/// <summary>Design-time factory for <c>dotnet ef migrations add</c> against the PostgreSQL tenant DbContext.</summary>
public sealed class PolarTenantDbContextPostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<PolarTenantDbContext>
{
    /// <summary>Builds a DbContext for the EF design-time tools.</summary>
    public PolarTenantDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarTenantDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=polar_design;Username=postgres",
                b => b.MigrationsAssembly(typeof(PolarTenantDbContextPostgreSqlDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new PolarTenantDbContext(options);
    }
}
