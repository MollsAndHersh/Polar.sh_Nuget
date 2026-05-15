using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.Extensions;

namespace PolarSharp.MultiTenant.Identity.SqlServer;

/// <summary>
/// SQL Server provider registration for PolarSharp Identity.
/// </summary>
/// <remarks>
/// <para>Three deployment shapes — pick one:</para>
/// <list type="number">
///   <item><description><see cref="UseSqlServer(PolarIdentityBuilder, string)"/> — explicit connection string.</description></item>
///   <item><description><see cref="UseSqlServer(PolarIdentityBuilder, IConfiguration)"/> — read from <c>PolarSharp:Identity:Sql</c> in <c>appsettings.json</c>.</description></item>
///   <item><description><see cref="HostDbContextRegistration.UseHostDbContext{TContext}"/> (in the base package) — share the host app's existing DbContext.</description></item>
/// </list>
/// </remarks>
public static class SqlServerIdentityBuilderExtensions
{
    /// <summary>
    /// Registers a SQL Server-backed <see cref="PolarUserDbContext"/> using an explicit
    /// connection string and wires up Identity services on top of it.
    /// </summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="connectionString">SQL Server connection string for the Identity database.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarIdentity(builder.Configuration)
    ///     .UseSqlServer("Server=...;Database=PolarIdentity;...")
    ///     .AddCoreIdentityServices();
    /// </code>
    /// </example>
    public static PolarIdentityBuilder UseSqlServer(this PolarIdentityBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        return RegisterDbContext(builder, connectionString);
    }

    /// <summary>
    /// Registers a SQL Server-backed <see cref="PolarUserDbContext"/> using the connection
    /// string resolved from <see cref="PolarIdentityOptions.SqlOptions"/> in
    /// <c>appsettings.json</c>.
    /// </summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="configuration">Application configuration — the bound options provide either a direct connection string or a <c>ConnectionStringName</c> pointer.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <remarks>
    /// <para>Resolution priority:</para>
    /// <list type="number">
    ///   <item><description><c>PolarSharp:Identity:Sql:ConnectionString</c> if non-empty</description></item>
    ///   <item><description>otherwise <c>ConnectionStrings:{PolarSharp:Identity:Sql:ConnectionStringName}</c></description></item>
    /// </list>
    /// <para>
    /// To share the host's main connection string, set <c>ConnectionStringName</c> to
    /// <c>"DefaultConnection"</c> (or whatever name the host uses).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="json">
    /// {
    ///   "ConnectionStrings": {
    ///     "DefaultConnection": "Server=...;Database=MyApp;..."
    ///   },
    ///   "PolarSharp": {
    ///     "Identity": {
    ///       "Sql": {
    ///         "Provider": "SqlServer",
    ///         "ConnectionStringName": "DefaultConnection"
    ///       }
    ///     }
    ///   }
    /// }
    /// </code>
    /// <code>
    /// builder.Services
    ///     .AddPolarIdentity(builder.Configuration)
    ///     .UseSqlServer(builder.Configuration)
    ///     .AddCoreIdentityServices();
    /// </code>
    /// </example>
    public static PolarIdentityBuilder UseSqlServer(this PolarIdentityBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(PolarIdentityOptions.SectionName).Get<PolarIdentityOptions>() ?? new PolarIdentityOptions();
        var connectionString = IdentityConnectionResolver.Resolve(null, configuration, options.Sql);
        return RegisterDbContext(builder, connectionString);
    }

    private static PolarIdentityBuilder RegisterDbContext(PolarIdentityBuilder builder, string connectionString)
    {
        // V20-008 Layer 2: same session interceptor the multi-tenant base package wires
        // into PolarTenantDbContext. Registering here too so PolarUserDbContext's RLS
        // policies see SESSION_CONTEXT(N'tenant_id') + (N'is_app_master_admin') per request.
        builder.Services.AddScoped<global::PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer.SqlServerTenantSessionInterceptor>();

        builder.Services.AddDbContext<PolarUserDbContext>((sp, opts) =>
            opts.UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(SqlServerIdentityBuilderExtensions).Assembly.GetName().Name))
                .AddInterceptors(sp.GetRequiredService<global::PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer.SqlServerTenantSessionInterceptor>()));

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PolarUserDbContext>(
                name: "polar-identity-sql",
                tags: ["polar-sql", "polar-identity"]);

        builder.AddCoreIdentityServices();
        return builder;
    }
}
