namespace PolarSharp.Reporting.Reports;

// ── 1. Cross-tenant revenue rollup ─────────────────────────────────────────

/// <summary>Request for <c>GetCrossTenantRevenueAsync</c>. Gated by <c>[RequireAppMasterAdmin]</c>.</summary>
public sealed record CrossTenantRevenueRequest
{
    /// <summary>Inclusive range start.</summary>
    public required DateTimeOffset From { get; init; }
    /// <summary>Exclusive range end.</summary>
    public required DateTimeOffset To { get; init; }
    /// <summary>How many top tenants to break out individually. The rest collapse into an "Other" row. Default 20.</summary>
    public int TopTenants { get; init; } = 20;
}

/// <summary>Platform-wide revenue rollup with per-tenant breakdown.</summary>
public sealed record CrossTenantRevenueReport
{
    /// <summary>Sum of every tenant's gross revenue in the range.</summary>
    public required long PlatformGrossRevenue { get; init; }
    /// <summary>Sum of every tenant's net revenue in the range.</summary>
    public required long PlatformNetRevenue { get; init; }
    /// <summary>Total tenants with any activity in the range.</summary>
    public required int ActiveTenantCount { get; init; }
    /// <summary>Top tenants by revenue, descending. The last row may be the synthetic "Other" aggregate of remaining tenants when their count exceeds <c>TopTenants</c>.</summary>
    public required IReadOnlyList<TenantRevenueRow> ByTenant { get; init; }
}

/// <summary>One tenant's revenue slice in a cross-tenant rollup.</summary>
public sealed record TenantRevenueRow(string TenantId, long GrossRevenue, long NetRevenue, int OrderCount);

// ── 2. Cross-tenant order volume ───────────────────────────────────────────

/// <summary>Request for <c>GetCrossTenantOrderVolumeAsync</c>. Gated by <c>[RequireAppMasterAdmin]</c>.</summary>
public sealed record CrossTenantOrderVolumeRequest
{
    /// <summary>Inclusive range start.</summary>
    public required DateTimeOffset From { get; init; }
    /// <summary>Exclusive range end.</summary>
    public required DateTimeOffset To { get; init; }
    /// <summary>Cap on per-tenant rows returned. Default 50.</summary>
    public int Limit { get; init; } = 50;
}

/// <summary>Order volume per tenant — useful for "which tenants are most active right now".</summary>
public sealed record CrossTenantOrderVolumeReport(IReadOnlyList<TenantOrderVolumeRow> Rows);

/// <summary>One tenant's order volume row.</summary>
public sealed record TenantOrderVolumeRow(string TenantId, int OrderCount, int CustomerCount, DateTimeOffset? LastOrderAt);

// ── 3. Webhook delivery health per tenant ──────────────────────────────────

/// <summary>Request for <c>GetWebhookDeliveryHealthAsync</c>. Gated by <c>[RequireAppMasterAdmin]</c>. Reads from the Events snapshot table.</summary>
public sealed record WebhookDeliveryHealthRequest
{
    /// <summary>Inclusive range start.</summary>
    public required DateTimeOffset From { get; init; }
    /// <summary>Exclusive range end.</summary>
    public required DateTimeOffset To { get; init; }
}

/// <summary>Per-tenant webhook activity summary.</summary>
public sealed record WebhookDeliveryHealthReport(IReadOnlyList<WebhookDeliveryHealthRow> Rows);

/// <summary>One tenant's webhook health row.</summary>
public sealed record WebhookDeliveryHealthRow(
    string TenantId,
    int TotalEvents,
    int OrderCreatedEvents,
    int SubscriptionEvents,
    int RefundEvents,
    DateTimeOffset? LastEventAt);

// ── 4. Tenant health rollup ────────────────────────────────────────────────

/// <summary>Request for <c>GetTenantHealthAsync</c>. Gated by <c>[RequireAppMasterAdmin]</c>.</summary>
public sealed record TenantHealthRequest
{
    /// <summary>The "recent activity" window. Tenants with no orders in this window are flagged Dormant. Default 30 days back from now.</summary>
    public required DateTimeOffset RecentActivityCutoff { get; init; }
}

/// <summary>Per-tenant composite health snapshot.</summary>
public sealed record TenantHealthReport(IReadOnlyList<TenantHealthRow> Rows);

/// <summary>One tenant's health rollup.</summary>
public sealed record TenantHealthRow(
    string TenantId,
    TenantHealthGrade Grade,
    int RecentOrderCount,
    int TotalCustomerCount,
    int ActiveSubscriptionCount,
    DateTimeOffset? LastOrderAt);

/// <summary>Coarse-grained tenant health classification.</summary>
public enum TenantHealthGrade
{
    /// <summary>Active orders in the recent window AND at least one customer.</summary>
    Healthy,
    /// <summary>Customers but no recent orders.</summary>
    Dormant,
    /// <summary>No customers AND no orders — newly onboarded or stalled.</summary>
    Empty,
}
