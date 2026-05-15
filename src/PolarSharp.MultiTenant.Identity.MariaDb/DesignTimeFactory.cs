using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.MultiTenant.Identity.MariaDb;

/// <summary>Design-time factory for <c>dotnet ef migrations add</c> against the Identity DbContext (MariaDB / MySQL).</summary>
public sealed class PolarUserDbContextMariaDbDesignTimeFactory : IDesignTimeDbContextFactory<PolarUserDbContext>
{
    /// <inheritdoc/>
    public PolarUserDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarUserDbContext>()
            .UseMySQL(
                "Server=design-time;Database=polar_identity_design;User Id=design;Password=design;",
                b => b.MigrationsAssembly(typeof(PolarUserDbContextMariaDbDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new PolarUserDbContext(options);
    }
}
