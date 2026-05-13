using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.Extensions;

namespace PolarSharp.MultiTenant.Identity.Sqlite;

/// <summary>
/// SQLite provider registration for PolarSharp Identity.
/// </summary>
/// <remarks>
/// <para>
/// Identity tables live in a single shared SQLite file (the host's main DB or a dedicated
/// Identity DB) — unlike catalog data, Identity is NOT split per-tenant since user identities
/// are global. Cross-tenant scope is enforced via the membership row's tenant filter.
/// </para>
/// <para>Three deployment shapes — pick one:</para>
/// <list type="number">
///   <item><description><see cref="UseSqlite(PolarIdentityBuilder, string)"/> — explicit connection string.</description></item>
///   <item><description><see cref="UseSqlite(PolarIdentityBuilder, IConfiguration)"/> — read from <c>PolarSharp:Identity:Sql</c> in <c>appsettings.json</c>.</description></item>
///   <item><description><see cref="HostDbContextRegistration.UseHostDbContext{TContext}"/> (in the base package) — share the host app's existing DbContext.</description></item>
/// </list>
/// </remarks>
public static class SqliteIdentityBuilderExtensions
{
    /// <summary>Registers a SQLite-backed <see cref="PolarUserDbContext"/> using an explicit connection string.</summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="connectionString">SQLite connection string (typically <c>Data Source=path/to/identity.db</c>).</param>
    /// <returns>The same builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarIdentity(builder.Configuration)
    ///     .UseSqlite("Data Source=./data/polar-identity.db")
    ///     .AddCoreIdentityServices();
    /// </code>
    /// </example>
    public static PolarIdentityBuilder UseSqlite(this PolarIdentityBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        return RegisterDbContext(builder, connectionString);
    }

    /// <summary>Registers a SQLite-backed <see cref="PolarUserDbContext"/> using a connection string resolved from configuration.</summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="configuration">Application configuration — see <see cref="PolarIdentityOptions.SqlOptions"/> for resolution rules.</param>
    public static PolarIdentityBuilder UseSqlite(this PolarIdentityBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(PolarIdentityOptions.SectionName).Get<PolarIdentityOptions>() ?? new PolarIdentityOptions();
        var connectionString = IdentityConnectionResolver.Resolve(null, configuration, options.Sql);
        return RegisterDbContext(builder, connectionString);
    }

    private static PolarIdentityBuilder RegisterDbContext(PolarIdentityBuilder builder, string connectionString)
    {
        builder.Services.AddDbContext<PolarUserDbContext>(opts =>
            opts.UseSqlite(connectionString, sql =>
                sql.MigrationsAssembly(typeof(SqliteIdentityBuilderExtensions).Assembly.GetName().Name)));

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PolarUserDbContext>(
                name: "polar-identity-sql",
                tags: ["polar-sql", "polar-identity"]);

        builder.AddCoreIdentityServices();
        return builder;
    }
}
