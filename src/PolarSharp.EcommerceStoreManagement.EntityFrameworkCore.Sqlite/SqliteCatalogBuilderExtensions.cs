using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Sqlite;

/// <summary>SQLite provider for the local catalog database — uses one <c>{tenantId}.db</c> file per tenant for filesystem-level isolation.</summary>
public static class SqliteCatalogBuilderExtensions
{
    /// <summary>Registers a SQLite-backed <see cref="PolarCatalogDbContext"/>.</summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="connectionString">SQLite connection string (typically <c>"Data Source=path/to/catalog.db"</c>).</param>
    public static IServiceCollection UseSqliteCatalog(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        services.AddDbContext<PolarCatalogDbContext>(opts =>
            opts.UseSqlite(connectionString, sql =>
                sql.MigrationsAssembly(typeof(SqliteCatalogBuilderExtensions).Assembly.GetName().Name)));
        services.AddHealthChecks()
            .AddDbContextCheck<PolarCatalogDbContext>(name: "polar-catalog-sql", tags: ["polar-sql", "polar-catalog"]);
        return services;
    }

    /// <summary>Registers a SQLite-backed catalog DbContext, reading the connection string from configuration.</summary>
    public static IServiceCollection UseSqliteCatalog(this IServiceCollection services, IConfiguration configuration)
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
        return services.UseSqliteCatalog(connectionString);
    }
}
