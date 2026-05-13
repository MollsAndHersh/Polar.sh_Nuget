using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>DI helpers for registering <see cref="PolarMigrationRunner{TContext}"/>.</summary>
public static class MigrationRunnerExtensions
{
    /// <summary>
    /// Registers a <see cref="PolarMigrationRunner{TContext}"/> hosted service for the
    /// supplied <typeparamref name="TContext"/>. Run on host startup, applies any pending
    /// migrations idempotently. Configure via <see cref="PolarMigrationOptions"/>.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to migrate.</typeparam>
    /// <param name="services">The DI service collection.</param>
    /// <param name="options">Optional inline configuration callback.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection RunPolarMigrationsAtStartup<TContext>(
        this IServiceCollection services,
        Action<PolarMigrationOptions>? options = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(sp =>
        {
            var opts = new PolarMigrationOptions();
            options?.Invoke(opts);
            return opts;
        });
        services.AddHostedService<PolarMigrationRunner<TContext>>();
        return services;
    }
}
