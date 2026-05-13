namespace PolarSharp.Reporting.Reports;

/// <summary>Aggregate order metrics — counts and fulfilment-latency stats.</summary>
public sealed record OrderReport
{
    /// <summary>Total orders in the period.</summary>
    public required int Total { get; init; }
    /// <summary>Orders that have been fulfilled (Polar <c>fulfilled_at</c> is set).</summary>
    public required int Fulfilled { get; init; }
    /// <summary>Orders pending fulfilment.</summary>
    public required int Pending { get; init; }
    /// <summary>Orders that failed (payment-failed, charge-rejected, etc.).</summary>
    public required int Failed { get; init; }
    /// <summary>Median time from <c>created_at</c> to <c>fulfilled_at</c>.</summary>
    public required TimeSpan MedianFulfillmentLatency { get; init; }
    /// <summary>Per-bucket counts for charting.</summary>
    public IReadOnlyList<OrderTimeBucket> TimeBuckets { get; init; } = [];
}

/// <summary>One bucket on the orders time-series chart.</summary>
public sealed record OrderTimeBucket(DateTimeOffset BucketStart, int Total, int Fulfilled, int Failed);

/// <summary>Request shape for <see cref="IPolarReportingClient.GetOrdersAsync"/>.</summary>
public sealed record OrderReportRequest
{
    /// <summary>Inclusive start.</summary>
    public required DateTimeOffset PeriodStart { get; init; }
    /// <summary>Exclusive end.</summary>
    public required DateTimeOffset PeriodEnd { get; init; }
    /// <summary>Bucket granularity for <see cref="OrderReport.TimeBuckets"/>.</summary>
    public TimeBucketGranularity Granularity { get; init; } = TimeBucketGranularity.Daily;
}
