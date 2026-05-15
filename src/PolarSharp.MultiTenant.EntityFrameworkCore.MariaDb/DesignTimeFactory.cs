using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb;

/// <summary>Design-time factory for <c>dotnet ef migrations add</c> against the MariaDB / MySQL tenant DbContext.</summary>
/// <remarks>
/// EF design-time tools never establish a real database connection — the connection string passed
/// here just needs to parse. Generated migrations are provider-shape-correct without ever
/// touching a live MariaDB instance.
/// </remarks>
public sealed class PolarTenantDbContextMariaDbDesignTimeFactory : IDesignTimeDbContextFactory<PolarTenantDbContext>
{
    /// <summary>Builds a DbContext for the EF design-time tools.</summary>
    public PolarTenantDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarTenantDbContext>()
            .UseMySQL(
                "Server=design-time;Database=polar_design;User Id=design;Password=design;",
                b => b.MigrationsAssembly(typeof(PolarTenantDbContextMariaDbDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new PolarTenantDbContext(options);
    }
}
