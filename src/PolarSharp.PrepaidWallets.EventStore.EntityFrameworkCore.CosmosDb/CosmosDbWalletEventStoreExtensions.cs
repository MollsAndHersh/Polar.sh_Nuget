using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.CosmosDb;

/// <summary>
/// Azure Cosmos DB provider for the wallet event store. Per-wallet partition key
/// (<c>/walletId</c>) with aggressive snapshot threshold (every 10 events vs. the
/// default 50 on relational providers) due to full-history replay RU cost.
/// </summary>
/// <remarks>Phase 21 ships the registration scaffold; full DbContext + index policies land in Phase 21.x.</remarks>
public static class CosmosDbWalletEventStoreExtensions
{
    /// <summary>Registers Cosmos DB as the wallet event store provider.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="accountEndpoint">Cosmos account endpoint URL.</param>
    /// <param name="accountKey">Cosmos account master key.</param>
    /// <param name="databaseName">Cosmos database name.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseCosmosDbWalletEventStore(this IServiceCollection services, string accountEndpoint, string accountKey, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(accountEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(accountKey);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        return services;
    }
}
