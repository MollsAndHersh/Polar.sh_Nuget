namespace PolarSharp.Reporting.Snapshot;

/// <summary>
/// Optional time-series snapshot service — periodically pulls Polar's
/// <c>/v1/events/</c> + <c>/v1/orders/</c> + <c>/v1/subscriptions/</c> + <c>/v1/customers/</c>
/// and mirrors them into local SQL. Once enabled, every reporting query reads from local
/// indexed SQL instead of paginating Polar — drilldowns load in milliseconds.
/// </summary>
public interface IReportSnapshotService
{
    /// <summary>Pulls every changed Polar resource since the per-tenant checkpoint and applies the deltas to local SQL.</summary>
    /// <param name="tenantId">The tenant to snapshot.</param>
    /// <param name="ct">Cancellation.</param>
    Task<SnapshotReport> RunSnapshotAsync(string tenantId, CancellationToken ct = default);
}

/// <summary>Per-tenant snapshot run output.</summary>
public sealed record SnapshotReport(
    int EventsIngested,
    int OrdersIngested,
    int OrderLineItemsIngested,
    int OrderRefundsIngested,
    int SubscriptionsIngested,
    int CustomersIngested,
    int BenefitGrantsIngested,
    TimeSpan Duration);

/// <summary>Bound from <c>PolarSharp:Reporting</c> in appsettings.</summary>
public sealed class PolarReportingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PolarSharp:Reporting";

    /// <summary>When true, the <c>PolarReportingHostedService</c> background snapshotter runs. Default true.</summary>
    public bool EnableSnapshot { get; set; } = true;

    /// <summary>How often the snapshotter runs. Default 15 minutes.</summary>
    public TimeSpan SnapshotInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Bounded parallelism — concurrent snapshot work across tenants. Default 4.</summary>
    public int MaxTenantsInParallel { get; set; } = 4;

    /// <summary>Snapshot rows older than this are pruned. Default 365 days.</summary>
    public int SnapshotRetentionDays { get; set; } = 365;

    /// <summary>IANA time-zone name for bucket alignment in time-series reports. Default UTC.</summary>
    public string ReportTimeZone { get; set; } = "UTC";
}
