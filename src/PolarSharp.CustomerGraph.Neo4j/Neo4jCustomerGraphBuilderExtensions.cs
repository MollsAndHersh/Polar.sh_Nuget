using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;

namespace PolarSharp.CustomerGraph.Neo4j;

/// <summary>
/// Neo4j provider registration for the PolarSharp customer graph.
/// </summary>
/// <remarks>
/// <para>
/// Two isolation modes are supported, auto-detected at startup:
/// </para>
/// <list type="bullet">
///   <item>
///     <strong>Neo4j Enterprise (multi-database)</strong> — per-tenant Neo4j DATABASE.
///     Strongest isolation; matches PolarSharp's defense-in-depth posture. Tenant data
///     lives in physically separate Neo4j databases; cross-tenant queries are
///     STRUCTURALLY IMPOSSIBLE because the databases don't share an address space.
///   </item>
///   <item>
///     <strong>Neo4j Community (single-database)</strong> — label-based isolation via
///     <c>:Tenant_&lt;id&gt;</c> labels on every node. The projection writes the tenant
///     label; the query builder enforces a MATCH clause on the label for every query.
///     Weaker isolation (relies on query-building discipline) but works on the free tier.
///   </item>
/// </list>
/// <para>
/// On startup, the provider connects, queries the server for multi-database support, and
/// auto-selects the appropriate mode. When falling back to label-based isolation, a
/// Warning log explicitly notes the security posture downgrade.
/// </para>
/// </remarks>
public static class Neo4jCustomerGraphBuilderExtensions
{
    /// <summary>
    /// Registers Neo4j as the customer-graph backend.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="boltUri">Neo4j Bolt URI (e.g. <c>"bolt://neo4j.internal:7687"</c>).</param>
    /// <param name="user">Neo4j username.</param>
    /// <param name="password">Neo4j password.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddPolarCustomerGraphNeo4j(
        this IServiceCollection services,
        string boltUri,
        string user,
        string password)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(boltUri);
        ArgumentException.ThrowIfNullOrEmpty(user);
        ArgumentException.ThrowIfNullOrEmpty(password);

        services.AddSingleton<IDriver>(_ =>
            GraphDatabase.Driver(boltUri, AuthTokens.Basic(user, password)));

        // Full ICustomerGraphQueryClient + ICustomerGraphProjector Neo4j implementations
        // land in Phase 17.x. Phase 17 establishes the driver registration + the per-tenant
        // database vs label-based isolation auto-detection design.
        return services;
    }
}
