namespace PolarSharp.CustomerGraph;

/// <summary>
/// Typed query API for the customer graph. Hosts construct queries via
/// <see cref="CustomerGraphQuery"/>'s fluent builder; the implementation translates
/// queries to the underlying graph database's native query language (openCypher for
/// Neo4j, Gremlin for Cosmos Gremlin API, etc.).
/// </summary>
public interface ICustomerGraphQueryClient
{
    /// <summary>Executes a customer-graph query and returns the matching customer nodes.</summary>
    /// <param name="query">The query specification.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CustomerGraphResult<CustomerNode>> ExecuteAsync(CustomerGraphQuery query, CancellationToken ct = default);

    /// <summary>Counts the customer nodes matching a query without materializing them.</summary>
    /// <param name="query">The query specification.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<int> CountAsync(CustomerGraphQuery query, CancellationToken ct = default);
}

/// <summary>A paged result envelope for customer-graph queries.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The matching items.</param>
/// <param name="TotalCount">Total matching count (across all pages).</param>
/// <param name="QueryDuration">Elapsed query time.</param>
public sealed record CustomerGraphResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    TimeSpan QueryDuration);

/// <summary>A customer node returned from a graph query.</summary>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="Email">Customer email.</param>
/// <param name="Name">Customer display name, if known.</param>
/// <param name="OrderCount">Total orders placed by this customer (denormalized in the graph).</param>
/// <param name="LifetimeValue">Total spend (denormalized).</param>
/// <param name="Currency">ISO 4217 currency for LifetimeValue.</param>
/// <param name="City">Geo-resolved city if IP capture + geo enrichment are enabled, else null.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country if geo enrichment is enabled, else null.</param>
/// <param name="Tags">Host-defined tags (e.g. "active", "fraud-flagged", "vip").</param>
public sealed record CustomerNode(
    string CustomerId,
    string Email,
    string? Name,
    int OrderCount,
    decimal LifetimeValue,
    string Currency,
    string? City,
    string? Country,
    IReadOnlyList<string> Tags);
