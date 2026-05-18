using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

/// <summary>
/// DI registration helpers for the single-tenant -> multi-tenant upgrade infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This package supplies the provider-agnostic orchestrator
/// (<see cref="SingleTenantUpgradeHostedService"/>), the configuration / options validation
/// pipeline, and the default <see cref="ITenantRegistryUpgrader"/>. Each provider package
/// (SqlServer / Sqlite / PostgreSQL / MariaDb / Cosmos) registers its own
/// <see cref="ISingleTenantUpgradeMigrator"/> implementation in addition — without one the
/// orchestrator throws at startup with a clear error.
/// </para>
/// </remarks>
public static class SingleTenantUpgradeServiceCollectionExtensions
{
    /// <summary>
    /// Adds the single-tenant -> multi-tenant upgrade infrastructure. Provider packages MUST
    /// register an <see cref="ISingleTenantUpgradeMigrator"/> implementation separately
    /// (typically via their own <c>UseXxxUpgrade()</c> extension method).
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">
    /// The application configuration root. Bound to
    /// <see cref="SingleTenantUpgradeOptions.SectionName"/>
    /// (<c>PolarSharp:MultiTenant:SingleTenantUpgrade</c>).
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services
    ///     .AddPolarMultiTenant(...)
    ///     .AddPolarSingleTenantUpgrade(builder.Configuration);
    /// // Provider package adds the migrator:
    /// services.AddPolarSingleTenantUpgradeSqlite();
    /// </code>
    /// </example>
    public static IServiceCollection AddPolarSingleTenantUpgrade(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SingleTenantUpgradeOptions>()
            .Bind(configuration.GetSection(SingleTenantUpgradeOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<SingleTenantUpgradeOptions>, SingleTenantUpgradeOptionsValidator>());

        services.TryAddScoped<ITenantRegistryUpgrader, DefaultTenantRegistryUpgrader>();

        services.AddHostedService<SingleTenantUpgradeHostedService>();

        return services;
    }
}
