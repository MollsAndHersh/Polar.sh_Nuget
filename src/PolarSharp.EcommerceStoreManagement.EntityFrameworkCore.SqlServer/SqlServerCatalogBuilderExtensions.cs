using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer;

/// <summary>SQL Server provider for the local catalog database.</summary>
/// <remarks>
/// Shared-database deployment with EF Core query filters + SQL Server RLS policies for
/// tenant isolation. The RLS policies are added by the EF migrations in this package.
/// </remarks>
public static class SqlServerCatalogBuilderExtensions
{
    /// <summary>Registers a SQL Server-backed <see cref="PolarCatalogDbContext"/>.</summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="connectionString">SQL Server connection string for the catalog database.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection UseSqlServerCatalog(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        services.AddDbContext<PolarCatalogDbContext>(opts =>
            opts.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(SqlServerCatalogBuilderExtensions).Assembly.GetName().Name)));
        services.AddHealthChecks()
            .AddDbContextCheck<PolarCatalogDbContext>(name: "polar-catalog-sql", tags: ["polar-sql", "polar-catalog"]);
        return services;
    }

    /// <summary>Registers a SQL Server-backed catalog DbContext, resolving the connection string from <c>PolarSharp:EcommerceStoreManagement:Sql:ConnectionString</c> or <c>:ConnectionStringName</c>.</summary>
    public static IServiceCollection UseSqlServerCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("PolarSharp:EcommerceStoreManagement:Sql");
        var direct = section["ConnectionString"];
        var named = section["ConnectionStringName"];
        var connectionString = !string.IsNullOrWhiteSpace(direct)
            ? direct
            : !string.IsNullOrWhiteSpace(named) ? configuration.GetConnectionString(named) : null;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Catalog SQL connection string not configured. Set PolarSharp:EcommerceStoreManagement:Sql:ConnectionString or :ConnectionStringName.");
        return services.UseSqlServerCatalog(connectionString);
    }
}
