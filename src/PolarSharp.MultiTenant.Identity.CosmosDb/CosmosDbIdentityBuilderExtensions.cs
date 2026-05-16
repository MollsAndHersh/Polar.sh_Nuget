using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.Extensions;

namespace PolarSharp.MultiTenant.Identity.CosmosDb;

/// <summary>
/// Azure Cosmos DB provider registration for PolarSharp Identity.
/// </summary>
/// <remarks>
/// <para>
/// Cosmos isolation works via <strong>per-tenant partition keys</strong> rather than row-level
/// security or per-file separation. Identity tables (users, roles, claims, memberships) all
/// partition on the tenant id; per-tenant queries are single-partition reads (cheap); cross-tenant
/// queries are cross-partition (RU-expensive) and rejected on the <c>[AllowCrossTenant]</c>
/// filter when Cosmos is the active provider.
/// </para>
/// <para>
/// Cosmos has no schema migration concept; the Identity DbContext relies on
/// <c>EnsureCreatedAsync</c> at host startup to provision the database + containers.
/// </para>
/// </remarks>
public static class CosmosDbIdentityBuilderExtensions
{
    /// <summary>Registers a Cosmos DB-backed <see cref="PolarUserDbContext"/> using account endpoint + key.</summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="accountEndpoint">Cosmos account endpoint URL.</param>
    /// <param name="accountKey">Cosmos account master key.</param>
    /// <param name="databaseName">Cosmos database name.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PolarIdentityBuilder UseCosmosDb(this PolarIdentityBuilder builder, string accountEndpoint, string accountKey, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(accountEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(accountKey);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        return RegisterDbContext(builder, opts => opts.UseCosmos(accountEndpoint, accountKey, databaseName));
    }

    /// <summary>Registers a Cosmos DB-backed <see cref="PolarUserDbContext"/> using a connection string.</summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="connectionString">Cosmos connection string.</param>
    /// <param name="databaseName">Cosmos database name.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PolarIdentityBuilder UseCosmosDb(this PolarIdentityBuilder builder, string connectionString, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        return RegisterDbContext(builder, opts => opts.UseCosmos(connectionString, databaseName));
    }

    /// <summary>Registers a Cosmos DB-backed <see cref="PolarUserDbContext"/> using configuration.</summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="configuration">Application configuration; reads <c>PolarSharp:Identity:Sql:ConnectionString</c> + <c>:DatabaseName</c>.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the connection string or database name is not configured.</exception>
    public static PolarIdentityBuilder UseCosmosDb(this PolarIdentityBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("PolarSharp:Identity:Sql");
        var connectionString = section["ConnectionString"];
        var databaseName = section["DatabaseName"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Cosmos DB Identity provider requires PolarSharp:Identity:Sql:ConnectionString and :DatabaseName to be configured.");
        return RegisterDbContext(builder, opts => opts.UseCosmos(connectionString, databaseName));
    }

    private static PolarIdentityBuilder RegisterDbContext(PolarIdentityBuilder builder, Action<DbContextOptionsBuilder> configureOptions)
    {
        builder.Services.AddDbContext<PolarUserDbContext>(configureOptions);

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PolarUserDbContext>(
                name: "polar-identity-sql",
                tags: ["polar-sql", "polar-identity", "polar-cosmosdb"]);

        builder.AddCoreIdentityServices();
        return builder;
    }
}
