using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c> / <c>dotnet ef dbcontext optimize</c>
/// to construct the tenant DbContext WITHOUT requiring a live host. Migrations only reflect
/// over the EF model — they don't open a real connection — so the placeholder connection
/// string is fine.
/// </summary>
public sealed class PolarTenantDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<PolarTenantDbContext>
{
    /// <summary>Builds a DbContext for the EF design-time tools.</summary>
    public PolarTenantDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarTenantDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=PolarSharp.MultiTenant.Design;",
                b => b.MigrationsAssembly(typeof(PolarTenantDbContextSqlServerDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new PolarTenantDbContext(options);
    }
}
