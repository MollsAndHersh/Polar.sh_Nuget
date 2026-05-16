using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.PostgreSQL;

/// <summary>
/// PostgreSQL provider for the wallet event store (alternative to the Marten event-store
/// when the host wants EF Core semantics over their existing Postgres infrastructure).
/// </summary>
/// <remarks>Phase 21 ships the registration scaffold; full DbContext + migrations land in Phase 21.x.</remarks>
public static class PostgreSqlWalletEventStoreExtensions
{
    /// <summary>Registers PostgreSQL as the wallet event store provider.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="connectionString">Npgsql connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UsePostgreSqlWalletEventStore(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        return services;
    }
}
