using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite;

/// <summary>Design-time factory for <c>dotnet ef migrations add</c> against the SQLite tenant DbContext.</summary>
public sealed class PolarTenantDbContextSqliteDesignTimeFactory : IDesignTimeDbContextFactory<PolarTenantDbContext>
{
    /// <summary>Builds a DbContext for the EF design-time tools.</summary>
    public PolarTenantDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarTenantDbContext>()
            .UseSqlite(
                "Data Source=design-time.db",
                b => b.MigrationsAssembly(typeof(PolarTenantDbContextSqliteDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new PolarTenantDbContext(options);
    }
}
