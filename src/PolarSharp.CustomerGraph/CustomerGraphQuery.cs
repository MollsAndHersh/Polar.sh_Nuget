namespace PolarSharp.CustomerGraph;

/// <summary>
/// Fluent builder for customer-graph queries. Hosts compose predicates declaratively;
/// the <see cref="ICustomerGraphQueryClient"/> implementation translates the composed
/// query into the underlying graph database's native query language.
/// </summary>
/// <example>
/// <code>
/// // "Find every active customer who has used the same IP as the known-fraud account,
/// //  ordered by lifetime value, top 100":
/// var query = new CustomerGraphQuery()
///     .WhereSharesIpWith("fraud-2026-04")
///     .WhereTaggedAs("active")
///     .OrderByLifetimeValue(descending: true)
///     .Top(100);
/// var result = await graphClient.ExecuteAsync(query, ct);
/// </code>
/// </example>
public sealed class CustomerGraphQuery
{
    private readonly List<ICustomerGraphPredicate> _predicates = [];
    private string? _orderByField;
    private bool _orderDescending;
    private int? _top;

    /// <summary>Filters to customers who have purchased the given product (any quantity, any time).</summary>
    public CustomerGraphQuery WhereCustomerBought(string productId)
    {
        ArgumentException.ThrowIfNullOrEmpty(productId);
        _predicates.Add(new CustomerBoughtPredicate([productId], RequireAll: false));
        return this;
    }

    /// <summary>Filters to customers who have purchased any of the given products.</summary>
    public CustomerGraphQuery WhereCustomerBoughtAny(IEnumerable<string> productIds)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        _predicates.Add(new CustomerBoughtPredicate([.. productIds], RequireAll: false));
        return this;
    }

    /// <summary>Filters to customers who have purchased ALL of the given products.</summary>
    public CustomerGraphQuery WhereCustomerBoughtAll(IEnumerable<string> productIds)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        _predicates.Add(new CustomerBoughtPredicate([.. productIds], RequireAll: true));
        return this;
    }

    /// <summary>Filters to customers in the given city (requires geo enrichment).</summary>
    public CustomerGraphQuery WhereInCity(string city)
    {
        ArgumentException.ThrowIfNullOrEmpty(city);
        _predicates.Add(new InCityPredicate(city));
        return this;
    }

    /// <summary>Filters to customers in the given country (ISO 3166-1 alpha-2; requires geo enrichment).</summary>
    public CustomerGraphQuery WhereInCountry(string isoCountryCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(isoCountryCode);
        _predicates.Add(new InCountryPredicate(isoCountryCode));
        return this;
    }

    /// <summary>Filters to customers whose lifetime spend exceeds the threshold.</summary>
    public CustomerGraphQuery WhereSpentMoreThan(decimal amount)
    {
        _predicates.Add(new SpentRangePredicate(amount, null));
        return this;
    }

    /// <summary>Filters to customers whose lifetime spend is in the given range.</summary>
    public CustomerGraphQuery WhereSpentBetween(decimal min, decimal max)
    {
        _predicates.Add(new SpentRangePredicate(min, max));
        return this;
    }

    /// <summary>Constrains the time window for activity-based predicates (purchases, IP sightings, etc.).</summary>
    public CustomerGraphQuery InLastDays(int days)
    {
        if (days <= 0) throw new ArgumentOutOfRangeException(nameof(days), "Must be positive.");
        _predicates.Add(new TimeWindowPredicate(days));
        return this;
    }

    /// <summary>
    /// Fraud-detection primitive: filters to customers who share at least one IP address
    /// with the specified customer. Requires IP capture (hashed or raw) enabled per-tenant
    /// AND the customer-graph projection has propagated the IP edges.
    /// </summary>
    public CustomerGraphQuery WhereSharesIpWith(string customerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(customerId);
        _predicates.Add(new SharesIpWithPredicate(customerId));
        return this;
    }

    /// <summary>Filters to customers tagged with the given host-defined tag (e.g. "active", "vip", "fraud-flagged").</summary>
    public CustomerGraphQuery WhereTaggedAs(string tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);
        _predicates.Add(new TaggedAsPredicate(tag));
        return this;
    }

    /// <summary>Limits results to the top N matches.</summary>
    public CustomerGraphQuery Top(int n)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Must be positive.");
        _top = n;
        return this;
    }

    /// <summary>Orders results by lifetime value (default descending).</summary>
    public CustomerGraphQuery OrderByLifetimeValue(bool descending = true)
    {
        _orderByField = "LifetimeValue";
        _orderDescending = descending;
        return this;
    }

    /// <summary>The composed predicates (read-only view for the query translator).</summary>
    public IReadOnlyList<ICustomerGraphPredicate> Predicates => _predicates;

    /// <summary>The optional order-by field name.</summary>
    public string? OrderByField => _orderByField;

    /// <summary>Whether the order-by is descending.</summary>
    public bool OrderDescending => _orderDescending;

    /// <summary>The optional Top(N) limit.</summary>
    public int? TopN => _top;
}

/// <summary>Marker interface for query predicates; query translators pattern-match concrete types.</summary>
public interface ICustomerGraphPredicate { }

/// <summary>Predicate: customer purchased any of (or all of) the listed products.</summary>
/// <param name="ProductIds">Product ids to match.</param>
/// <param name="RequireAll">When true, customer must have purchased ALL listed products; when false, ANY.</param>
public sealed record CustomerBoughtPredicate(IReadOnlyList<string> ProductIds, bool RequireAll) : ICustomerGraphPredicate;

/// <summary>Predicate: customer's geo-resolved city matches.</summary>
/// <param name="City">City name (exact match; requires geo enrichment in projection).</param>
public sealed record InCityPredicate(string City) : ICustomerGraphPredicate;

/// <summary>Predicate: customer's geo-resolved country matches.</summary>
/// <param name="IsoCountryCode">ISO 3166-1 alpha-2 country code.</param>
public sealed record InCountryPredicate(string IsoCountryCode) : ICustomerGraphPredicate;

/// <summary>Predicate: customer's lifetime spend is within a range.</summary>
/// <param name="MinAmount">Minimum (inclusive); null = no minimum.</param>
/// <param name="MaxAmount">Maximum (inclusive); null = no maximum.</param>
public sealed record SpentRangePredicate(decimal? MinAmount, decimal? MaxAmount) : ICustomerGraphPredicate;

/// <summary>Predicate: constrains time-based predicates to the last N days.</summary>
/// <param name="Days">Number of days back from now.</param>
public sealed record TimeWindowPredicate(int Days) : ICustomerGraphPredicate;

/// <summary>Predicate: customer shares at least one IP address with the specified customer (fraud-detection).</summary>
/// <param name="OtherCustomerId">The customer to find shared-IP neighbors for.</param>
public sealed record SharesIpWithPredicate(string OtherCustomerId) : ICustomerGraphPredicate;

/// <summary>Predicate: customer carries a host-defined tag.</summary>
/// <param name="Tag">The tag string (case-sensitive).</param>
public sealed record TaggedAsPredicate(string Tag) : ICustomerGraphPredicate;
