using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.CosmosDb;

/// <summary>
/// Azure Cosmos DB provider for the local catalog database.
/// </summary>
/// <remarks>
/// <para>
/// Catalog entities partition on TenantId; per-tenant catalog reads are single-partition
/// (cheap in Request Units); cross-tenant queries are rejected on the <c>[AllowCrossTenant]</c>
/// filter when Cosmos is the active provider.
/// </para>
/// <para>
/// Cosmos has no schema migration concept; catalog DbContext relies on
/// <c>EnsureCreatedAsync</c> at host startup to provision containers + indexing policies.
/// Schema additions (new fields on existing entities) are automatic; field removals require
/// manual data migration.
/// </para>
/// <para>
/// EF Core's Cosmos provider does NOT translate <c>JOIN</c> operations; the storefront's
/// hierarchical drilldown (Customer → Orders → LineItems) MUST run against snapshot
/// pre-aggregates from <c>PolarSharp.Reporting</c> when Cosmos is the catalog provider.
/// </para>
/// </remarks>
public static class CosmosDbCatalogBuilderExtensions
{
    /// <summary>Registers a Cosmos DB-backed <see cref="PolarCatalogDbContext"/>.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="accountEndpoint">Cosmos account endpoint URL.</param>
    /// <param name="accountKey">Cosmos account master key.</param>
    /// <param name="databaseName">Cosmos database name.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseCosmosDbCatalog(this IServiceCollection services, string accountEndpoint, string accountKey, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(accountEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(accountKey);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);

        services.AddDbContext<PolarCatalogDbContext>((sp, opts) =>
        {
            opts.UseCosmos(accountEndpoint, accountKey, databaseName);
            var auditInterceptor = sp.GetService<AuditLogSaveChangesInterceptor>();
            if (auditInterceptor is not null) opts.AddInterceptors(auditInterceptor);
        });
        services.AddHealthChecks()
            .AddDbContextCheck<PolarCatalogDbContext>(name: "polar-catalog-sql", tags: ["polar-sql", "polar-catalog", "polar-cosmosdb"]);
        return services;
    }

    /// <summary>Registers a Cosmos DB-backed catalog DbContext, reading credentials from configuration.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">Reads <c>PolarSharp:EcommerceStoreManagement:Sql:ConnectionString</c> + <c>:DatabaseName</c>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration is missing.</exception>
    public static IServiceCollection UseCosmosDbCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("PolarSharp:EcommerceStoreManagement:Sql");
        var connectionString = section["ConnectionString"];
        var databaseName = section["DatabaseName"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Cosmos DB catalog provider requires PolarSharp:EcommerceStoreManagement:Sql:ConnectionString and :DatabaseName to be configured.");
        services.AddDbContext<PolarCatalogDbContext>((sp, opts) =>
        {
            opts.UseCosmos(connectionString, databaseName);
            var auditInterceptor = sp.GetService<AuditLogSaveChangesInterceptor>();
            if (auditInterceptor is not null) opts.AddInterceptors(auditInterceptor);
        });
        services.AddHealthChecks()
            .AddDbContextCheck<PolarCatalogDbContext>(name: "polar-catalog-sql", tags: ["polar-sql", "polar-catalog", "polar-cosmosdb"]);
        return services;
    }
}
