using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp;
using PolarSharp.MultiTenant.EntityFrameworkCore.Extensions;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer;

/// <summary>
/// SQL Server-specific registration extensions for the PolarSharp tenant store.
/// </summary>
public static class SqlServerBuilderExtensions
{
    /// <summary>
    /// Registers a SQL Server-backed EF Core tenant store, replacing the in-memory tenant
    /// registry that <see cref="MultiTenant.Extensions.MultiTenantBuilderExtensions.AddPolarMultiTenant"/>
    /// wires up by default.
    /// </summary>
    /// <param name="builder">The PolarSharp infrastructure builder returned by <c>AddPolarMultiTenant()</c>.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="seedFromAppSettings">
    /// When <see langword="true"/> and the tenants table is empty on first startup, copies
    /// every <c>PolarSharp:MultiTenant:Tenants[*]</c> entry from <c>appsettings.json</c> into
    /// the database (one-time migration helper). Default <see langword="false"/>.
    /// </param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarMultiTenant()
    ///     .UseSqlServer("Server=...;Database=PolarTenants;Trusted_Connection=true;");
    /// </code>
    /// </example>
    public static PolarInfrastructureBuilder UseSqlServer(
        this PolarInfrastructureBuilder builder,
        string connectionString,
        bool seedFromAppSettings = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        EfTenantStoreBuilderExtensions.AddCoreServices(builder.Services, builder.Configuration);

        // V20-008 Layer 2: session interceptor sets SESSION_CONTEXT('tenant_id') +
        // SESSION_CONTEXT('is_app_master_admin') on every connection open so the
        // EnableRowLevelSecurity migration's SECURITY POLICY enforces tenant isolation
        // at the DB layer (defense in depth alongside the EF query filter).
        builder.Services.AddScoped<SqlServerTenantSessionInterceptor>();

        builder.Services.AddDbContext<PolarTenantDbContext>((sp, opts) =>
            opts.UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(SqlServerBuilderExtensions).Assembly.GetName().Name))
                .AddInterceptors(sp.GetRequiredService<SqlServerTenantSessionInterceptor>()));
        builder.Services.AddScoped<IMultiTenantStore<PolarTenantInfo>, EfMultiTenantStore>();

        // EF Core health check
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
