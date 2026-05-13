using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL;

/// <summary>PostgreSQL provider for the local catalog database.</summary>
public static class PostgreSqlCatalogBuilderExtensions
{
    /// <summary>Registers a PostgreSQL-backed <see cref="PolarCatalogDbContext"/>.</summary>
    public static IServiceCollection UsePostgreSqlCatalog(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        services.AddDbContext<PolarCatalogDbContext>(opts => opts.UseNpgsql(connectionString));
        services.AddHealthChecks()
            .AddDbContextCheck<PolarCatalogDbContext>(name: "polar-catalog-sql", tags: ["polar-sql", "polar-catalog"]);
        return services;
    }

    /// <summary>Registers a PostgreSQL-backed catalog DbContext, reading the connection string from configuration.</summary>
    public static IServiceCollection UsePostgreSqlCatalog(this IServiceCollection services, IConfiguration configuration)
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
        return services.UsePostgreSqlCatalog(connectionString);
    }
}
