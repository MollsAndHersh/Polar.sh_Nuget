namespace PolarSharp.Reporting.Reports;

/// <summary>Aggregate customer roll-up.</summary>
public sealed record CustomerReport
{
    /// <summary>Total customers in the tenant.</summary>
    public required int TotalCustomers { get; init; }
    /// <summary>Customers first seen in the period.</summary>
    public required int NewCustomers { get; init; }
    /// <summary>Mean lifetime value (net revenue per customer) in minor units.</summary>
    public required long AverageLifetimeValue { get; init; }
    /// <summary>Breakdown of customers by lifecycle segment.</summary>
    public IReadOnlyList<CustomerLifecycleSegment> Segments { get; init; } = [];
}

/// <summary>One row in <see cref="CustomerReport.Segments"/>.</summary>
/// <param name="SegmentName">e.g. <c>"new"</c>, <c>"active"</c>, <c>"churned"</c>.</param>
/// <param name="CustomerCount">Customers in this segment.</param>
/// <param name="TotalRevenue">Revenue attributable to this segment (minor units).</param>
public sealed record CustomerLifecycleSegment(string SegmentName, int CustomerCount, long TotalRevenue);

/// <summary>Request shape for <see cref="IPolarReportingClient.GetCustomersAsync"/>.</summary>
public sealed record CustomerReportRequest
{
    /// <summary>Inclusive start.</summary>
    public required DateTimeOffset PeriodStart { get; init; }
    /// <summary>Exclusive end.</summary>
    public required DateTimeOffset PeriodEnd { get; init; }
}

/// <summary>Per-customer entitlement view — what benefits the named customer currently has access to.</summary>
public sealed record CustomerEntitlementsReport
{
    /// <summary>Polar customer id.</summary>
    public required string CustomerId { get; init; }
    /// <summary>Customer email (snapshotted).</summary>
    public required string CustomerEmail { get; init; }
    /// <summary>Currently-granted benefits.</summary>
    public IReadOnlyList<ActiveEntitlement> ActiveBenefits { get; init; } = [];
    /// <summary>Previously-granted benefits that have been revoked.</summary>
    public IReadOnlyList<RevokedEntitlement> RevokedBenefits { get; init; } = [];
}

/// <summary>An active benefit grant.</summary>
public sealed record ActiveEntitlement(string BenefitId, string BenefitName, string BenefitKind, DateTimeOffset GrantedAt);

/// <summary>A revoked benefit grant.</summary>
public sealed record RevokedEntitlement(string BenefitId, string BenefitName, DateTimeOffset RevokedAt, string Reason);
