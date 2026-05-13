using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.MultiTenant.Identity.PostgreSQL;

/// <summary>Design-time factory for <c>dotnet ef migrations add</c> against the Identity DbContext (PostgreSQL).</summary>
public sealed class PolarUserDbContextPostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<PolarUserDbContext>
{
    /// <inheritdoc/>
    public PolarUserDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarUserDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=polar_identity_design;Username=postgres",
                b => b.MigrationsAssembly(typeof(PolarUserDbContextPostgreSqlDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new PolarUserDbContext(options);
    }
}
