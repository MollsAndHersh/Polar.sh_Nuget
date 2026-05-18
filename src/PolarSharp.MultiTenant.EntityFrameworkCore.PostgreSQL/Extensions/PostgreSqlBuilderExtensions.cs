using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp;
using PolarSharp.MultiTenant.EntityFrameworkCore.Extensions;
using PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL.Upgrade;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL;

/// <summary>PostgreSQL-specific registration extensions for the PolarSharp tenant store.</summary>
public static class PostgreSqlBuilderExtensions
{
    /// <summary>
    /// Registers a PostgreSQL-backed EF Core tenant store, replacing the in-memory tenant
    /// registry that <c>AddPolarMultiTenant()</c> wires up by default.
    /// </summary>
    /// <param name="builder">The PolarSharp infrastructure builder.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
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
    ///     .UsePostgreSql("Host=...;Database=polar_tenants;Username=...;Password=...");
    /// </code>
    /// </example>
    public static PolarInfrastructureBuilder UsePostgreSql(
        this PolarInfrastructureBuilder builder,
        string connectionString,
        bool seedFromAppSettings = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        EfTenantStoreBuilderExtensions.AddCoreServices(builder.Services, builder.Configuration);

        // V20-008 Layer 2: session interceptor sets app.current_tenant_id +
        // app.is_app_master_admin on every connection open so the EnableRowLevelSecurity
        // migration's POLICY enforces tenant isolation at the DB layer (defense in depth
        // alongside the EF query filter).
        builder.Services.AddScoped<PostgreSqlTenantSessionInterceptor>();

        builder.Services.AddDbContext<PolarTenantDbContext>((sp, opts) =>
            opts.UseNpgsql(connectionString, npg =>
                    npg.MigrationsAssembly(typeof(PostgreSqlBuilderExtensions).Assembly.GetName().Name))
                .AddInterceptors(sp.GetRequiredService<PostgreSqlTenantSessionInterceptor>()));
        builder.Services.AddScoped<IMultiTenantStore<PolarTenantInfo>, EfMultiTenantStore>();

        // PostgreSQL-specific single-tenant -> MT upgrade migrator. Always registered;
        // only executed when the host has also called AddPolarSingleTenantUpgrade(...).
        builder.Services.TryAddScoped<ISingleTenantUpgradeMigrator, PostgreSqlSingleTenantUpgradeMigrator>();

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
