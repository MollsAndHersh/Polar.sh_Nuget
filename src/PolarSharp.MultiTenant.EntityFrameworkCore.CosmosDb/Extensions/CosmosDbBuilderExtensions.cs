using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp;
using PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb.Upgrade;
using PolarSharp.MultiTenant.EntityFrameworkCore.Extensions;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb;

/// <summary>
/// Azure Cosmos DB-specific registration extensions for the PolarSharp tenant store.
/// </summary>
/// <remarks>
/// <para>
/// Cosmos is a NoSQL document store; tenant isolation works fundamentally differently from
/// the SQL providers. Where Postgres / SQL Server use row-level security policies and the
/// SQLite provider uses physical per-tenant database files, the Cosmos provider uses Cosmos's
/// native <strong>logical partition key</strong> as the isolation primitive. Every tenant-owned
/// entity is configured with <c>HasPartitionKey(e =&gt; e.TenantId)</c>; queries scoped to a
/// single tenant are single-partition (cheap in Request Units); cross-tenant queries are
/// inherently cross-partition (RU-expensive) and are explicitly rejected by the
/// <c>[AllowCrossTenant]</c> filter when Cosmos is the active provider.
/// </para>
/// <para>
/// Cosmos has NO schema migration concept. Where the SQL providers ship EF Core migration
/// classes that <c>dotnet ef database update</c> applies, the Cosmos provider relies on
/// <c>DbContext.Database.EnsureCreatedAsync()</c> at host startup to create the database
/// + containers + indexing policies if they don't exist. Schema evolution (adding fields)
/// is automatic because Cosmos documents are schemaless; removing fields requires a manual
/// data migration outside EF Core.
/// </para>
/// <para>
/// Hierarchical drilldown queries (e.g., customer → orders → line items) DO NOT work
/// efficiently on Cosmos because EF Core's Cosmos provider does not translate <c>JOIN</c>
/// operations. Hosts running on Cosmos MUST enable PolarSharp.Reporting's snapshot service
/// with aggressive pre-aggregation so each level of the drilldown is a single-document read.
/// </para>
/// </remarks>
public static class CosmosDbBuilderExtensions
{
    /// <summary>
    /// Registers an Azure Cosmos DB-backed EF Core tenant store, replacing the in-memory
    /// tenant registry that <c>AddPolarMultiTenant()</c> wires up by default.
    /// </summary>
    /// <param name="builder">The PolarSharp infrastructure builder returned by <c>AddPolarMultiTenant()</c>.</param>
    /// <param name="accountEndpoint">Cosmos account endpoint URL (e.g. <c>"https://my-cosmos.documents.azure.com:443/"</c>).</param>
    /// <param name="accountKey">Cosmos account master key. Treat as secret; load from env / Key Vault, never hardcode.</param>
    /// <param name="databaseName">Name of the Cosmos database to use (created if absent on first startup).</param>
    /// <param name="seedFromAppSettings">
    /// When <see langword="true"/> and the tenants container is empty on first startup, copies
    /// every <c>PolarSharp:MultiTenant:Tenants[*]</c> entry from <c>appsettings.json</c>.
    /// </param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarMultiTenant()
    ///     .UseCosmosDb(
    ///         accountEndpoint: builder.Configuration["Polar:Cosmos:Endpoint"]!,
    ///         accountKey: builder.Configuration["Polar:Cosmos:Key"]!,
    ///         databaseName: "polar_tenants");
    /// </code>
    /// </example>
    public static PolarInfrastructureBuilder UseCosmosDb(
        this PolarInfrastructureBuilder builder,
        string accountEndpoint,
        string accountKey,
        string databaseName,
        bool seedFromAppSettings = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(accountEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(accountKey);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);

        EfTenantStoreBuilderExtensions.AddCoreServices(builder.Services, builder.Configuration);

        builder.Services.AddDbContext<PolarTenantDbContext>(opts =>
            opts.UseCosmos(accountEndpoint, accountKey, databaseName, cosmos =>
            {
                cosmos.ContentResponseOnWriteEnabled(true);
            }));

        builder.Services.AddScoped<IMultiTenantStore<PolarTenantInfo>, EfMultiTenantStore>();

        RegisterCosmosUpgradeServices(builder.Services, builder.Configuration);

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PolarTenantDbContext>(
                name: "polar-tenant-sql",
                tags: ["polar-sql", "polar-tenant", "polar-cosmosdb"]);

        if (seedFromAppSettings)
        {
            builder.Services.AddHostedService<AppSettingsSeeder>();
        }

        return builder;
    }

    /// <summary>
    /// Registers an Azure Cosmos DB-backed EF Core tenant store using a single connection
    /// string instead of separate endpoint + key.
    /// </summary>
    /// <param name="builder">The PolarSharp infrastructure builder.</param>
    /// <param name="connectionString">Cosmos connection string (<c>"AccountEndpoint=...;AccountKey=...;"</c>).</param>
    /// <param name="databaseName">Name of the Cosmos database.</param>
    /// <param name="seedFromAppSettings">Whether to seed from <c>appsettings.json</c> on first startup.</param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for chaining.</returns>
    public static PolarInfrastructureBuilder UseCosmosDb(
        this PolarInfrastructureBuilder builder,
        string connectionString,
        string databaseName,
        bool seedFromAppSettings = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);

        EfTenantStoreBuilderExtensions.AddCoreServices(builder.Services, builder.Configuration);

        builder.Services.AddDbContext<PolarTenantDbContext>(opts =>
            opts.UseCosmos(connectionString, databaseName, cosmos =>
            {
                cosmos.ContentResponseOnWriteEnabled(true);
            }));

        builder.Services.AddScoped<IMultiTenantStore<PolarTenantInfo>, EfMultiTenantStore>();

        RegisterCosmosUpgradeServices(builder.Services, builder.Configuration);

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PolarTenantDbContext>(
                name: "polar-tenant-sql",
                tags: ["polar-sql", "polar-tenant", "polar-cosmosdb"]);

        if (seedFromAppSettings)
        {
            builder.Services.AddHostedService<AppSettingsSeeder>();
        }

        return builder;
    }

    /// <summary>
    /// Registers Cosmos-specific upgrade services: the migrator, its options binding, and
    /// the hosted-service container provisioner that stands in for the relational
    /// providers' <c>AddUpgradeHistoryTable</c> EF migration (Cosmos has no migrations).
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">The application configuration root.</param>
    private static void RegisterCosmosUpgradeServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CosmosDbSingleTenantUpgradeOptions>()
            .Bind(configuration.GetSection(CosmosDbSingleTenantUpgradeOptions.SectionName));

        // Cosmos-specific single-tenant -> MT upgrade migrator. Always registered;
        // only executed when the host has also called AddPolarSingleTenantUpgrade(...).
        services.TryAddScoped<ISingleTenantUpgradeMigrator, CosmosDbSingleTenantUpgradeMigrator>();

        // Cosmos-equivalent of the AddUpgradeHistoryTable migration shipped by the
        // relational providers. Ensures the polar_upgrade_history container exists at
        // host startup. Always registered; safe to run on every boot (EnsureCreated is
        // a no-op when the container already exists).
        services.AddHostedService<CosmosUpgradeHistoryContainerProvisioner>();
    }
}
