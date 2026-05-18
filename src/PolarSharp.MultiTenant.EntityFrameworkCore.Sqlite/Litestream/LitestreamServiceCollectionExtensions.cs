using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

/// <summary>
/// DI registration helpers for the optional Litestream integration that ships with the
/// PolarSharp SQLite provider.
/// </summary>
public static class LitestreamServiceCollectionExtensions
{
    /// <summary>
    /// Adds optional Litestream integration support for the SQLite provider. The integration
    /// is opt-in via the
    /// <c>PolarSharp:MultiTenant:Sqlite:Litestream:UseLitestream</c> config flag — when
    /// <see langword="false"/>, none of the registered services do any work.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">
    /// Application configuration root. Bound to <see cref="LitestreamOptions.SectionName"/>
    /// (<c>PolarSharp:MultiTenant:Sqlite:Litestream</c>).
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Registers:
    /// <list type="bullet">
    ///   <item><see cref="LitestreamOptions"/> bound from configuration, with
    ///   <c>ValidateOnStart</c> so misconfiguration surfaces at boot rather than at first use.</item>
    ///   <item><see cref="LitestreamOptionsValidator"/> as <see cref="IValidateOptions{TOptions}"/>.</item>
    ///   <item>A named <see cref="HttpClient"/> for the health check
    ///   (<see cref="LitestreamHealthCheck.HttpClientName"/>).</item>
    ///   <item><see cref="LitestreamConfigGenerator"/> as a singleton.</item>
    ///   <item><see cref="LitestreamHealthCheck"/> as a health check tagged
    ///   <c>polar-sql</c> and <c>polar-litestream</c>.</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarMultiTenant()
    ///     .UseSqlite("/var/lib/polarsharp/tenants/");
    /// // The above already calls AddPolarSqliteLitestream by default. To opt in:
    /// // {"PolarSharp":{"MultiTenant":{"Sqlite":{"Litestream":{"UseLitestream":true,...}}}}}
    /// </code>
    /// </example>
    public static IServiceCollection AddPolarSqliteLitestream(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LitestreamOptions>()
            .Bind(configuration.GetSection(LitestreamOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<LitestreamOptions>, LitestreamOptionsValidator>());

        services.AddHttpClient(LitestreamHealthCheck.HttpClientName);

        services.TryAddSingleton<LitestreamConfigGenerator>();

        services.AddHealthChecks()
            .AddCheck<LitestreamHealthCheck>(
                name: "litestream",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["polar-sql", "polar-litestream"]);

        return services;
    }
}
