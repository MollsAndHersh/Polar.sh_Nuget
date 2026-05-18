using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PolarSharp.MultiTenant.Lifecycle;

/// <summary>DI registration helpers for the tenant lifecycle infrastructure.</summary>
/// <remarks>
/// <para>
/// Registers <see cref="ITenantStatusService"/> + its options + MediatR (so that
/// <see cref="TenantStatusChangedNotification"/> handlers in this assembly are discovered).
/// Hosts that already register MediatR with different assemblies remain compatible — MediatR
/// de-duplicates assembly scans, and the call here only adds the PolarSharp.MultiTenant
/// assembly to the handler-discovery set.
/// </para>
/// </remarks>
public static class TenantLifecycleServiceCollectionExtensions
{
    /// <summary>
    /// Adds the tenant lifecycle infrastructure: <see cref="ITenantStatusService"/>,
    /// <see cref="TenantStatusServiceOptions"/> bound from configuration, and MediatR
    /// (with handler scanning of the <c>PolarSharp.MultiTenant</c> assembly).
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">
    /// The application configuration root. Bound to
    /// <see cref="TenantStatusServiceOptions.SectionName"/>
    /// (<c>PolarSharp:MultiTenant:TenantStatus</c>).
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services
    ///     .AddPolarMultiTenant(...)
    ///     .AddPolarTenantLifecycle(builder.Configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddPolarTenantLifecycle(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TenantStatusServiceOptions>(
            configuration.GetSection(TenantStatusServiceOptions.SectionName));

        // MediatR de-duplicates assembly scans, so this is safe to call even if the host
        // already registered MediatR with their own assemblies elsewhere.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ITenantStatusService).Assembly));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ITenantStatusService, DefaultTenantStatusService>();

        return services;
    }
}
