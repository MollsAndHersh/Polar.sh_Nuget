using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.MariaDb;

/// <summary>
/// MariaDB / MySQL provider for the wallet event store. App-layer query filter only
/// (MariaDB lacks Postgres-style RLS); same posture as v1.3 MariaDB tenant store.
/// </summary>
/// <remarks>Phase 21 ships the registration scaffold; full DbContext + migrations land in Phase 21.x.</remarks>
public static class MariaDbWalletEventStoreExtensions
{
    /// <summary>Registers MariaDB / MySQL as the wallet event store provider.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="connectionString">ADO.NET-format MariaDB / MySQL connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseMariaDbWalletEventStore(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        return services;
    }
}
