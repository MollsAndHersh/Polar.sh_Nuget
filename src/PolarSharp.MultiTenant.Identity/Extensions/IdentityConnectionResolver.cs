using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.MultiTenant.Identity.Extensions;

/// <summary>
/// Resolves the Identity SQL connection string from a fluent argument, an
/// <see cref="IConfiguration"/> connection-string name, or the bound
/// <see cref="PolarIdentityOptions.SqlOptions"/>.
/// </summary>
public static class IdentityConnectionResolver
{
    /// <summary>Resolves a connection string using priority: explicit &gt; <see cref="IConfiguration"/> &gt; bound options.</summary>
    /// <param name="explicitConnectionString">Connection string passed directly to the fluent extension. May be <see langword="null"/>.</param>
    /// <param name="configuration">Application configuration — used to read <c>ConnectionStrings:{name}</c>.</param>
    /// <param name="boundOptions">Already-bound <see cref="PolarIdentityOptions.SqlOptions"/>. Provides <see cref="PolarIdentityOptions.SqlOptions.ConnectionString"/> or <see cref="PolarIdentityOptions.SqlOptions.ConnectionStringName"/> as fallbacks.</param>
    /// <returns>The resolved connection string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no source produces a non-empty connection string.</exception>
    public static string Resolve(string? explicitConnectionString, IConfiguration configuration, PolarIdentityOptions.SqlOptions boundOptions)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(boundOptions);

        if (!string.IsNullOrWhiteSpace(explicitConnectionString)) return explicitConnectionString;

        if (!string.IsNullOrWhiteSpace(boundOptions.ConnectionString)) return boundOptions.ConnectionString;

        if (!string.IsNullOrWhiteSpace(boundOptions.ConnectionStringName))
        {
            var fromConfig = configuration.GetConnectionString(boundOptions.ConnectionStringName);
            if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig;
            throw new InvalidOperationException(
                $"PolarSharp Identity: ConnectionStringName '{boundOptions.ConnectionStringName}' is set but no matching entry was found in ConnectionStrings.");
        }

        throw new InvalidOperationException(
            "PolarSharp Identity: no connection string was supplied. " +
            "Pass one to the UseSqlServer/UseSqlite/UsePostgreSql call, set PolarSharp:Identity:Sql:ConnectionString, " +
            "or set PolarSharp:Identity:Sql:ConnectionStringName to point at a ConnectionStrings entry. " +
            "If you intend to share the host's existing DbContext, call .UseHostDbContext<TContext>() instead.");
    }
}

/// <summary>
/// Shared "use the host's already-registered DbContext" wiring used by every provider
/// package's <c>UseHostDbContext&lt;TContext&gt;()</c> overload.
/// </summary>
public static class HostDbContextRegistration
{
    /// <summary>
    /// Configures PolarSharp Identity to resolve the <typeparamref name="TContext"/> the host
    /// has already registered, instead of registering a new <see cref="PolarUserDbContext"/>.
    /// </summary>
    /// <typeparam name="TContext">The host's DbContext type. Must inherit from
    /// <c>IdentityDbContext&lt;PolarApplicationUser, PolarApplicationRole, Guid&gt;</c> and
    /// either inherit from <see cref="PolarUserDbContext"/> or call
    /// <see cref="ModelBuilderExtensions.AddPolarIdentitySchema(ModelBuilder)"/> in its
    /// <c>OnModelCreating</c>.</typeparam>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <example>
    /// <code>
    /// // Host has its own DbContext that already includes PolarSharp Identity tables:
    /// builder.Services.AddDbContext&lt;ApplicationDbContext&gt;(opts =&gt;
    ///     opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    ///
    /// builder.Services
    ///     .AddPolarIdentity(builder.Configuration)
    ///     .UseHostDbContext&lt;ApplicationDbContext&gt;()
    ///     .AddCoreIdentityServices();
    /// </code>
    /// </example>
    public static PolarIdentityBuilder UseHostDbContext<TContext>(this PolarIdentityBuilder builder)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Forward PolarUserDbContext resolutions to the host's already-registered TContext.
        // Caller MUST register TContext themselves — typically via AddDbContext<TContext>() with
        // their own connection string, BEFORE calling AddPolarIdentity().
        builder.Services.AddScoped<PolarUserDbContext>(sp =>
        {
            var hostContext = sp.GetRequiredService<TContext>();
            if (hostContext is PolarUserDbContext direct) return direct;
            throw new InvalidOperationException(
                $"UseHostDbContext<{typeof(TContext).Name}>() requires {typeof(TContext).Name} to inherit from PolarUserDbContext. " +
                "Either change the host context's base class to PolarUserDbContext, or use a dedicated PolarUserDbContext via UseSqlServer()/UseSqlite()/UsePostgreSql().");
        });

        return builder;
    }
}
