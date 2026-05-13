using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.MultiTenant.Identity.Sqlite;

/// <summary>Design-time factory for <c>dotnet ef migrations add</c> against the Identity DbContext (SQLite).</summary>
public sealed class PolarUserDbContextSqliteDesignTimeFactory : IDesignTimeDbContextFactory<PolarUserDbContext>
{
    /// <inheritdoc/>
    public PolarUserDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarUserDbContext>()
            .UseSqlite(
                "Data Source=design-time.db",
                b => b.MigrationsAssembly(typeof(PolarUserDbContextSqliteDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new PolarUserDbContext(options);
    }
}
