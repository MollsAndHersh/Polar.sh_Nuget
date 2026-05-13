using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp;
using PolarSharp.MultiTenant.EntityFrameworkCore.Extensions;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite;

/// <summary>SQLite-specific registration extensions for the PolarSharp tenant store.</summary>
public static class SqliteBuilderExtensions
{
    /// <summary>
    /// Registers a SQLite-backed EF Core tenant store. The tenant registry lives in a shared
    /// <c>__tenants.db</c> file at <paramref name="databaseDirectory"/>; per-tenant catalog /
    /// identity / reporting DBs (when those packages are also installed) live in
    /// <c>{tenantId}.db</c> files in the same directory.
    /// </summary>
    /// <param name="builder">The PolarSharp infrastructure builder returned by <c>AddPolarMultiTenant()</c>.</param>
    /// <param name="databaseDirectory">Filesystem path where the SQLite database files live. Created if absent.</param>
    /// <param name="seedFromAppSettings">
    /// When <see langword="true"/> and the tenants table is empty on first startup, copies
    /// every <c>PolarSharp:MultiTenant:Tenants[*]</c> entry from <c>appsettings.json</c>.
    /// </param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarMultiTenant()
    ///     .UseSqlite("/var/lib/polarsharp/tenants/");
    /// </code>
    /// </example>
    public static PolarInfrastructureBuilder UseSqlite(
        this PolarInfrastructureBuilder builder,
        string databaseDirectory,
        bool seedFromAppSettings = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(databaseDirectory);

        Directory.CreateDirectory(databaseDirectory);
        var tenantStorePath = Path.Combine(databaseDirectory, "__tenants.db");
        var connectionString = $"Data Source={tenantStorePath};Cache=Shared;Mode=ReadWriteCreate";

        EfTenantStoreBuilderExtensions.AddCoreServices(builder.Services, builder.Configuration);

        builder.Services.AddDbContext<PolarTenantDbContext>(opts => opts.UseSqlite(connectionString));
        builder.Services.AddScoped<IMultiTenantStore<PolarTenantInfo>, EfMultiTenantStore>();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PolarTenantDbContext>(
                name: "polar-tenant-sql",
                tags: ["polar-sql", "polar-tenant"]);

        if (seedFromAppSettings)
        {
            builder.Services.AddHostedService<AppSettingsSeeder>();
        }

        return builder;
    }
}
