using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.MultiTenant.Identity.SqlServer;

/// <summary>Design-time factory for <c>dotnet ef migrations add</c> against the Identity DbContext (SQL Server).</summary>
public sealed class PolarUserDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<PolarUserDbContext>
{
    /// <inheritdoc/>
    public PolarUserDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarUserDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=PolarSharp.Identity.Design;",
                b => b.MigrationsAssembly(typeof(PolarUserDbContextSqlServerDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new PolarUserDbContext(options);
    }
}
