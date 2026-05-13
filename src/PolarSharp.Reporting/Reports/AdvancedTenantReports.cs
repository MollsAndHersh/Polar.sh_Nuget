namespace PolarSharp.Reporting.Reports;

// ── Granularity shared by every time-series report ─────────────────────────

/// <summary>Time-bucket granularity used by tenant time-series reports.</summary>
public enum ReportBucketGranularity
{
    /// <summary>One bucket per calendar day in the requested range.</summary>
    Daily,
    /// <summary>One bucket per ISO-8601 week (Monday-start) in the requested range.</summary>
    Weekly,
    /// <summary>One bucket per calendar month in the requested range.</summary>
    Monthly,
}

// ── 1. Revenue over time ───────────────────────────────────────────────────

/// <summary>Request for <c>GetRevenueOverTimeAsync</c>.</summary>
public sealed record RevenueOverTimeRequest
{
    /// <summary>Inclusive start of the reporting range.</summary>
    public required DateTimeOffset From { get; init; }
    /// <summary>Exclusive end of the reporting range.</summary>
    public required DateTimeOffset To { get; init; }
    /// <summary>Granularity of each bucket.</summary>
    public ReportBucketGranularity Granularity { get; init; } = ReportBucketGranularity.Daily;
}

/// <summary>Revenue / refunds / net-revenue series across the requested range.</summary>
public sealed record RevenueOverTimeReport
{
    /// <summary>Per-bucket totals, ordered ascending by <see cref="RevenueBucket.BucketStart"/>.</summary>
    public required IReadOnlyList<RevenueBucket> Buckets { get; init; }
    /// <summary>Sum of <see cref="RevenueBucket.GrossRevenue"/> across the range.</summary>
    public required long TotalGrossRevenue { get; init; }
    /// <summary>Sum of <see cref="RevenueBucket.NetRevenue"/> across the range.</summary>
    public required long TotalNetRevenue { get; init; }
    /// <summary>Total order count across the range.</summary>
    public required int TotalOrderCount { get; init; }
}

/// <summary>One time-bucket in a revenue series.</summary>
/// <param name="BucketStart">UTC start of the bucket.</param>
/// <param name="GrossRevenue">Sum of order Amount in minor units within the bucket.</param>
/// <param name="RefundedAmount">Sum of RefundedAmount in minor units within the bucket.</param>
/// <param name="NetRevenue"><c>GrossRevenue - RefundedAmount</c>.</param>
/// <param name="OrderCount">Number of orders created in the bucket.</param>
public sealed record RevenueBucket(DateTimeOffset BucketStart, long GrossRevenue, long RefundedAmount, long NetRevenue, int OrderCount);

// ── 2. Top products by revenue and units ───────────────────────────────────

/// <summary>Request for <c>GetTopProductsAsync</c>.</summary>
public sealed record TopProductsRequest
{
    /// <summary>Inclusive range start.</summary>
    public required DateTimeOffset From { get; init; }
    /// <summary>Exclusive range end.</summary>
    public required DateTimeOffset To { get; init; }
    /// <summary>How many results in each leaderboard. Default 10, capped at 100.</summary>
    public int Limit { get; init; } = 10;
}

/// <summary>Top-N products by revenue and by units sold within the range.</summary>
public sealed record TopProductsReport
{
    /// <summary>Top products by revenue, descending.</summary>
    public required IReadOnlyList<TopProductRow> ByRevenue { get; init; }
    /// <summary>Top products by units sold, descending.</summary>
    public required IReadOnlyList<TopProductRow> ByUnits { get; init; }
}

/// <summary>One row of a top-products leaderboard.</summary>
public sealed record TopProductRow(string PolarProductId, string ProductName, long Revenue, int UnitsSold, int OrderCount);

// ── 3. Top customers by lifetime value ─────────────────────────────────────

/// <summary>Request for <c>GetTopCustomersAsync</c>.</summary>
public sealed record TopCustomersRequest
{
    /// <summary>How many customers to return. Default 10, capped at 100.</summary>
    public int Limit { get; init; } = 10;
}

/// <summary>Top-N customers by lifetime value (uses the pre-aggregated <c>LifetimeValue</c> column).</summary>
public sealed record TopCustomersReport(IReadOnlyList<TopCustomerRow> Rows);

/// <summary>One customer in the top-customers leaderboard.</summary>
public sealed record TopCustomerRow(
    string PolarCustomerId,
    string Email,
    string? Name,
    long LifetimeValue,
    string Currency,
    int OrderCount,
    DateTimeOffset? FirstOrderAt,
    DateTimeOffset? LastOrderAt);

// ── 4. Subscription churn cohort ───────────────────────────────────────────

/// <summary>Request for <c>GetSubscriptionChurnCohortAsync</c>.</summary>
public sealed record SubscriptionChurnCohortRequest
{
    /// <summary>The cohort start (UTC, must be a month boundary). All subscriptions whose <c>StartedAt</c> falls in that month are the cohort.</summary>
    public required DateTimeOffset CohortMonth { get; init; }
    /// <summary>How many trailing months to evaluate retention against. Default 12.</summary>
    public int MonthsToTrack { get; init; } = 12;
}

/// <summary>One cohort's retention curve.</summary>
public sealed record SubscriptionChurnCohortReport
{
    /// <summary>UTC start of the cohort month.</summary>
    public required DateTimeOffset CohortMonth { get; init; }
    /// <summary>Size of the cohort (subscriptions started in the cohort month).</summary>
    public required int CohortSize { get; init; }
    /// <summary>Per-month rows: how many of the cohort were still active at each subsequent month boundary.</summary>
    public required IReadOnlyList<ChurnRetentionPoint> Retention { get; init; }
}

/// <summary>One retention measurement for a cohort.</summary>
/// <param name="MonthsAfterCohort">0 = cohort month, 1 = month after, etc.</param>
/// <param name="Active">Subscriptions still active at this point.</param>
/// <param name="Canceled">Subscriptions in the cohort that have been canceled by this point.</param>
/// <param name="RetentionPercent">Active / CohortSize × 100.</param>
public sealed record ChurnRetentionPoint(int MonthsAfterCohort, int Active, int Canceled, decimal RetentionPercent);

// ── 5. Refund rate over time ───────────────────────────────────────────────

/// <summary>Request for <c>GetRefundRateAsync</c>.</summary>
public sealed record RefundRateRequest
{
    /// <summary>Inclusive range start.</summary>
    public required DateTimeOffset From { get; init; }
    /// <summary>Exclusive range end.</summary>
    public required DateTimeOffset To { get; init; }
    /// <summary>Bucket granularity. Default Monthly.</summary>
    public ReportBucketGranularity Granularity { get; init; } = ReportBucketGranularity.Monthly;
}

/// <summary>Refund-rate time series — refund count and amount as a percentage of gross.</summary>
public sealed record RefundRateReport(IReadOnlyList<RefundRateBucket> Buckets, decimal OverallRefundRatePercent);

/// <summary>One bucket in the refund-rate series.</summary>
public sealed record RefundRateBucket(
    DateTimeOffset BucketStart,
    long GrossRevenue,
    long RefundedAmount,
    int OrderCount,
    int RefundCount,
    decimal RefundRatePercent);

// ── 6. Average order value over time ───────────────────────────────────────

/// <summary>Request for <c>GetAverageOrderValueAsync</c>.</summary>
public sealed record AverageOrderValueRequest
{
    /// <summary>Inclusive range start.</summary>
    public required DateTimeOffset From { get; init; }
    /// <summary>Exclusive range end.</summary>
    public required DateTimeOffset To { get; init; }
    /// <summary>Bucket granularity. Default Monthly.</summary>
    public ReportBucketGranularity Granularity { get; init; } = ReportBucketGranularity.Monthly;
}

/// <summary>Average-order-value time series.</summary>
public sealed record AverageOrderValueReport(IReadOnlyList<AverageOrderValueBucket> Buckets, decimal OverallAverage);

/// <summary>One bucket in the AOV series.</summary>
public sealed record AverageOrderValueBucket(DateTimeOffset BucketStart, int OrderCount, decimal AverageOrderValue);

// ── 7. Customer lifetime-value distribution ────────────────────────────────

/// <summary>Request for <c>GetCustomerLifetimeValueDistributionAsync</c>. Buckets are integer-edged: a customer with LTV X lands in the first bucket where <c>UpperBoundExclusive &gt; X</c>.</summary>
public sealed record CustomerLifetimeValueDistributionRequest
{
    /// <summary>Bucket boundaries in minor units. e.g. <c>[5_000, 25_000, 100_000, 500_000]</c> creates 5 buckets: [0, 5k), [5k, 25k), [25k, 100k), [100k, 500k), [500k, ∞).</summary>
    public required IReadOnlyList<long> BucketUpperBoundsExclusive { get; init; }
}

/// <summary>LTV histogram across the tenant's customers.</summary>
public sealed record CustomerLifetimeValueDistributionReport(IReadOnlyList<LtvBucket> Buckets);

/// <summary>One LTV bucket.</summary>
public sealed record LtvBucket(long? LowerBoundInclusive, long? UpperBoundExclusive, int CustomerCount, long TotalLifetimeValue);

// ── 8. Currency mix ────────────────────────────────────────────────────────

/// <summary>Request for <c>GetCurrencyMixAsync</c>.</summary>
public sealed record CurrencyMixRequest
{
    /// <summary>Inclusive range start.</summary>
    public required DateTimeOffset From { get; init; }
    /// <summary>Exclusive range end.</summary>
    public required DateTimeOffset To { get; init; }
}

/// <summary>Revenue and order count grouped by currency.</summary>
public sealed record CurrencyMixReport(IReadOnlyList<CurrencyMixRow> Rows);

/// <summary>One currency's slice of activity.</summary>
public sealed record CurrencyMixRow(string Currency, long GrossRevenue, long NetRevenue, int OrderCount, decimal SharePercent);
