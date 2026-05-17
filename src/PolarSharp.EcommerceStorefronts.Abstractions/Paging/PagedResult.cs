namespace PolarSharp.EcommerceStorefronts.Abstractions.Paging;

/// <summary>
/// Generic page envelope returned by storefront list / search / drilldown queries.
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
/// <remarks>
/// Lift-safe duplicate of the type that <c>PolarSharp.Reporting</c> defines under
/// <c>PolarSharp.Reporting.Drilldown</c>. The storefront-core packages cannot reference
/// <c>PolarSharp.Reporting</c> per Case Study 01, so the page envelope is mirrored here.
/// Bridges that adapt reporting data into storefront services translate between the two
/// shapes one-to-one.
/// </remarks>
public sealed record PagedResult<T>
{
    /// <summary>The page of rows.</summary>
    public required IReadOnlyList<T> Rows { get; init; }

    /// <summary>Total row count across all pages.</summary>
    public required int TotalCount { get; init; }

    /// <summary>0-based page index.</summary>
    public required int Page { get; init; }

    /// <summary>Rows per page used to compute the page.</summary>
    public required int PageSize { get; init; }

    /// <summary>True when at least one more page exists after this one.</summary>
    public bool HasMore => (Page + 1) * PageSize < TotalCount;
}
