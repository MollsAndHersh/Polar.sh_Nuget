namespace PolarSharp.Reporting.Reports;

/// <summary>
/// Operational error / audit report — aggregates PolarSharp's own observability metrics
/// (webhook delivery failures, signature-verification rejects, circuit-breaker events,
/// rate-limit hits, Polar API errors) PLUS the recent entries from Polar's own
/// <c>/v1/events/</c> stream.
/// </summary>
public sealed record ErrorAuditReport
{
    /// <summary>Number of webhook deliveries that failed to be processed.</summary>
    public required int WebhookDeliveryFailures { get; init; }
    /// <summary>Number of webhook payloads rejected for invalid HMAC signature.</summary>
    public required int SignatureVerificationFailures { get; init; }
    /// <summary>Times the resilience pipeline's circuit breaker opened.</summary>
    public required int CircuitBreakerOpenEvents { get; init; }
    /// <summary>Times Polar's rate limiter rejected our requests.</summary>
    public required int RateLimitHits { get; init; }
    /// <summary>Count of Polar API responses with non-2xx status (across all status codes).</summary>
    public required int ApiErrorsByStatus { get; init; }
    /// <summary>Recent platform events Polar's <c>/v1/events/</c> exposed for this organization.</summary>
    public IReadOnlyList<PolarEventLogEntry> RecentPolarEvents { get; init; } = [];
}

/// <summary>One row in <see cref="ErrorAuditReport.RecentPolarEvents"/>.</summary>
/// <param name="EventId">Polar event id (<c>evt_xxx</c>).</param>
/// <param name="Type">Polar event-type slug (e.g. <c>"order.created"</c>).</param>
/// <param name="OccurredAt">UTC when Polar recorded the event.</param>
/// <param name="Summary">Short host-rendered summary for display.</param>
public sealed record PolarEventLogEntry(string EventId, string Type, DateTimeOffset OccurredAt, string Summary);

/// <summary>Request shape for <see cref="IPolarReportingClient.GetErrorAuditAsync"/>.</summary>
public sealed record ErrorAuditRequest
{
    /// <summary>Inclusive start.</summary>
    public required DateTimeOffset PeriodStart { get; init; }
    /// <summary>Exclusive end.</summary>
    public required DateTimeOffset PeriodEnd { get; init; }
    /// <summary>How many recent Polar events to include. Default 50.</summary>
    public int RecentEventCount { get; init; } = 50;
}
