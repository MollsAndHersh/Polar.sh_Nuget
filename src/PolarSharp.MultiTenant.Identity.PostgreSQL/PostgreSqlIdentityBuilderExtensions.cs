using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.Extensions;

namespace PolarSharp.MultiTenant.Identity.PostgreSQL;

/// <summary>
/// PostgreSQL provider registration for PolarSharp Identity.
/// </summary>
/// <remarks>
/// <para>Three deployment shapes — pick one:</para>
/// <list type="number">
///   <item><description><see cref="UsePostgreSql(PolarIdentityBuilder, string)"/> — explicit connection string.</description></item>
///   <item><description><see cref="UsePostgreSql(PolarIdentityBuilder, IConfiguration)"/> — read from <c>PolarSharp:Identity:Sql</c> in <c>appsettings.json</c>.</description></item>
///   <item><description><see cref="HostDbContextRegistration.UseHostDbContext{TContext}"/> (in the base package) — share the host app's existing DbContext.</description></item>
/// </list>
/// </remarks>
public static class PostgreSqlIdentityBuilderExtensions
{
    /// <summary>Registers a PostgreSQL-backed <see cref="PolarUserDbContext"/> using an explicit connection string.</summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="connectionString">Npgsql-format PostgreSQL connection string.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarIdentity(builder.Configuration)
    ///     .UsePostgreSql("Host=localhost;Database=polar_identity;Username=app;Password=...")
    ///     .AddCoreIdentityServices();
    /// </code>
    /// </example>
    public static PolarIdentityBuilder UsePostgreSql(this PolarIdentityBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        return RegisterDbContext(builder, connectionString);
    }

    /// <summary>Registers a PostgreSQL-backed <see cref="PolarUserDbContext"/> using a connection string resolved from configuration.</summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="configuration">Application configuration — see <see cref="PolarIdentityOptions.SqlOptions"/> for resolution rules.</param>
    public static PolarIdentityBuilder UsePostgreSql(this PolarIdentityBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(PolarIdentityOptions.SectionName).Get<PolarIdentityOptions>() ?? new PolarIdentityOptions();
        var connectionString = IdentityConnectionResolver.Resolve(null, configuration, options.Sql);
        return RegisterDbContext(builder, connectionString);
    }

    private static PolarIdentityBuilder RegisterDbContext(PolarIdentityBuilder builder, string connectionString)
    {
        // V20-008 Layer 2: see SqlServer counterpart docs.
        builder.Services.AddScoped<global::PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL.PostgreSqlTenantSessionInterceptor>();

        builder.Services.AddDbContext<PolarUserDbContext>((sp, opts) =>
            opts.UseNpgsql(connectionString, npg =>
                    npg.MigrationsAssembly(typeof(PostgreSqlIdentityBuilderExtensions).Assembly.GetName().Name))
                .AddInterceptors(sp.GetRequiredService<global::PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL.PostgreSqlTenantSessionInterceptor>()));

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PolarUserDbContext>(
                name: "polar-identity-sql",
                tags: ["polar-sql", "polar-identity"]);

        builder.AddCoreIdentityServices();
        return builder;
    }
}
