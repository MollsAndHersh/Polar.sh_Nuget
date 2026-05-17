namespace PolarSharp.EcommerceStorefronts.Abstractions.Customers;

/// <summary>Query parameters for <see cref="IStorefrontCustomerService.ListOrdersAsync"/>.</summary>
public sealed record ListOrdersQuery
{
    /// <summary>0-based page index.</summary>
    public int Page { get; init; }

    /// <summary>Rows per page.</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Optional lower bound on order placement date.</summary>
    public DateTimeOffset? PlacedAfter { get; init; }

    /// <summary>Optional upper bound on order placement date.</summary>
    public DateTimeOffset? PlacedBefore { get; init; }

    /// <summary>
    /// Optional Polar wire-format status filter (<c>pending</c> | <c>paid</c> |
    /// <c>refunded</c> | <c>partially_refunded</c> | <c>void</c>).
    /// </summary>
    public string? Status { get; init; }
}
