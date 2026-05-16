using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.SqlServer;

/// <summary>
/// SQL Server provider for the wallet event store.
/// </summary>
/// <remarks>
/// Phase 21 ships the registration scaffold; full DbContext + migrations for
/// <c>wallet_events</c> + <c>wallet_snapshots</c> + projection tables land in Phase 21.x.
/// </remarks>
public static class SqlServerWalletEventStoreExtensions
{
    /// <summary>Registers SQL Server as the wallet event store provider.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseSqlServerWalletEventStore(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        // Phase 21.x: register WalletEventStoreDbContext with UseSqlServer + interceptor wiring.
        return services;
    }
}
