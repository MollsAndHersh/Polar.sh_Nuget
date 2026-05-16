using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Sqlite;

/// <summary>
/// SQLite provider for the wallet event store. Per-tenant <c>.db</c> file isolation matches
/// the v1.2 SQLite catalog provider's per-tenant-file approach.
/// </summary>
/// <remarks>Phase 21 ships the registration scaffold; full DbContext + migrations land in Phase 21.x.</remarks>
public static class SqliteWalletEventStoreExtensions
{
    /// <summary>Registers SQLite as the wallet event store provider.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="databaseDirectory">Filesystem path for the per-tenant SQLite database files.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseSqliteWalletEventStore(this IServiceCollection services, string databaseDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(databaseDirectory);
        return services;
    }
}
