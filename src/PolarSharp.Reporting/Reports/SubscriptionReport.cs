namespace PolarSharp.Reporting.Reports;

/// <summary>Aggregate subscription metrics — MRR / ARR / churn for SaaS / recurring-revenue tenants.</summary>
public sealed record SubscriptionReport
{
    /// <summary>Monthly Recurring Revenue at the end of the period (minor units).</summary>
    public required long Mrr { get; init; }
    /// <summary>Annual Recurring Revenue (MRR × 12).</summary>
    public required long Arr { get; init; }
    /// <summary>Subscriptions in <c>active</c> or <c>trialing</c> status at period end.</summary>
    public required int ActiveSubscriptions { get; init; }
    /// <summary>New subscriptions started in the period.</summary>
    public required int NewSubscriptions { get; init; }
    /// <summary>Subscriptions cancelled in the period.</summary>
    public required int CanceledSubscriptions { get; init; }
    /// <summary>Churn rate (cancelled ÷ start-of-period active). 0.05 = 5%.</summary>
    public required decimal ChurnRate { get; init; }
    /// <summary>Revenue from upgrades (existing subscriptions changing to higher-tier products).</summary>
    public required long ExpansionRevenue { get; init; }
    /// <summary>Per-cohort retention curves.</summary>
    public IReadOnlyList<CohortRetention> Cohorts { get; init; } = [];
}

/// <summary>Retention curve for one signup cohort.</summary>
/// <param name="CohortMonth">First day of the cohort's signup month.</param>
/// <param name="StartingCustomers">Customer count at month 0.</param>
/// <param name="RetainedByMonth">Retained-customer count for months 1..N (RetainedByMonth[0] = month 1).</param>
public sealed record CohortRetention(DateTimeOffset CohortMonth, int StartingCustomers, IReadOnlyList<int> RetainedByMonth);

/// <summary>Request shape for <see cref="IPolarReportingClient.GetSubscriptionsAsync"/>.</summary>
public sealed record SubscriptionReportRequest
{
    /// <summary>Inclusive start.</summary>
    public required DateTimeOffset PeriodStart { get; init; }
    /// <summary>Exclusive end.</summary>
    public required DateTimeOffset PeriodEnd { get; init; }
    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }
    /// <summary>How many months of cohort-retention data to include.</summary>
    public int CohortMonths { get; init; } = 12;
}
