namespace PolarSharp.Reporting.Reports;

/// <summary>Aggregate transaction roll-up for a single tenant over a date range.</summary>
public sealed record TransactionReport
{
    /// <summary>UTC start of the period (inclusive).</summary>
    public required DateTimeOffset PeriodStart { get; init; }
    /// <summary>UTC end of the period (exclusive).</summary>
    public required DateTimeOffset PeriodEnd { get; init; }
    /// <summary>ISO 4217 currency code (reports are single-currency to keep totals comparable).</summary>
    public required string Currency { get; init; }
    /// <summary>Total revenue in minor units before refunds.</summary>
    public required long GrossRevenue { get; init; }
    /// <summary>Total amount refunded in minor units.</summary>
    public required long RefundedAmount { get; init; }
    /// <summary>Net revenue (<see cref="GrossRevenue"/> − <see cref="RefundedAmount"/>) in minor units.</summary>
    public required long NetRevenue { get; init; }
    /// <summary>Number of orders in the period.</summary>
    public required int OrderCount { get; init; }
    /// <summary>Number of refunds issued in the period.</summary>
    public required int RefundCount { get; init; }
    /// <summary>Average order value (<see cref="GrossRevenue"/> ÷ <see cref="OrderCount"/>) in minor units.</summary>
    public required long AverageOrderValue { get; init; }
    /// <summary>Top-grossing products in the period.</summary>
    public IReadOnlyList<TopProduct> TopProducts { get; init; } = [];
    /// <summary>Per-bucket revenue for charting (daily / weekly / monthly per the request).</summary>
    public IReadOnlyList<TransactionTimeBucket> TimeBuckets { get; init; } = [];
}

/// <summary>One row in the top-products list.</summary>
public sealed record TopProduct(string ProductId, string ProductName, long Revenue, int OrderCount);

/// <summary>One bucket on the time-series chart.</summary>
public sealed record TransactionTimeBucket(DateTimeOffset BucketStart, long GrossRevenue, long NetRevenue, int OrderCount);

/// <summary>Request shape for <see cref="IPolarReportingClient.GetTransactionsAsync"/>.</summary>
public sealed record TransactionReportRequest
{
    /// <summary>Inclusive start.</summary>
    public required DateTimeOffset PeriodStart { get; init; }
    /// <summary>Exclusive end.</summary>
    public required DateTimeOffset PeriodEnd { get; init; }
    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }
    /// <summary>Bucket granularity for <see cref="TransactionReport.TimeBuckets"/>.</summary>
    public TimeBucketGranularity Granularity { get; init; } = TimeBucketGranularity.Daily;
    /// <summary>How many entries to include in <see cref="TransactionReport.TopProducts"/>.</summary>
    public int TopProductCount { get; init; } = 10;
}

/// <summary>Granularity for time-series report buckets.</summary>
public enum TimeBucketGranularity
{
    /// <summary>Daily buckets.</summary>
    Daily,
    /// <summary>Weekly buckets (ISO-week aligned).</summary>
    Weekly,
    /// <summary>Monthly buckets.</summary>
    Monthly,
}
