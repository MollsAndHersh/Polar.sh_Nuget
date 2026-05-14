using System.Threading.Channels;

namespace PolarSharp.Reporting.Snapshot;

/// <summary>
/// V20-005 Phase 2: per-tenant snapshot driver. Replaces the time-based platform sweep
/// pattern with on-demand triggers tied to user activity — snapshots run for tenants
/// who are actively using the app, not for every tenant every 15 minutes.
/// </summary>
/// <remarks>
/// <para>
/// API call volume scales with concurrent active users, not customer count. A tenant
/// with no active session contributes zero Polar API calls. The host's auth handler
/// calls <see cref="StartPeriodicAsync"/> on login; the reporting page's middleware
/// calls <see cref="Heartbeat"/> on each request to keep the per-tenant timer alive;
/// the host's auth handler calls <see cref="StopPeriodicAsync"/> on logout; the
/// idle-timeout cancels polling for sessions that go silent (closed-laptop scenario).
/// </para>
/// <para>
/// See <c>DESIGN-V20-005-PER-TENANT-SNAPSHOT.md</c> at the repo root for the full
/// design rationale, the 5 corner decisions, and the v1.4 RCL UI affordances this
/// interface is shaped to support.
/// </para>
/// </remarks>
public interface IReportSnapshotTrigger
{
    /// <summary>
    /// Fires an immediate snapshot for the tenant. Idempotent — concurrent calls for the
    /// same tenant deduplicate against the in-flight snapshot (the second call waits and
    /// receives the same completion event). For fire-and-forget UX, the caller can ignore
    /// the returned <see cref="Task"/>.
    /// </summary>
    /// <param name="tenantId">Tenant whose data to snapshot.</param>
    /// <param name="reason">Free-form short string captured in the
    /// <see cref="SnapshotCompletedEvent.Reason"/> for diagnostics ("Login" / "PageMount" /
    /// "ManualRefresh" / "PostMutation"). The <c>"ManualRefresh"</c> reason triggers
    /// debounce behavior — repeated calls within
    /// <see cref="SnapshotTriggerOptions.ManualRefreshDebounce"/> return the most recent
    /// completion event without firing a new snapshot.</param>
    /// <param name="ct">Cancellation.</param>
    Task<SnapshotCompletedEvent> TriggerImmediateAsync(string tenantId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Begins periodic per-tenant snapshotting on the supplied interval. Repeats until
    /// <see cref="StopPeriodicAsync"/> is called or the idle-timeout elapses since the
    /// last <see cref="Heartbeat"/>. Calling on an already-active tenant resets the idle
    /// clock and updates the interval if different. The first tick fires immediately, so
    /// callers don't need to ALSO call <see cref="TriggerImmediateAsync"/> on login.
    /// </summary>
    Task StartPeriodicAsync(string tenantId, TimeSpan interval);

    /// <summary>
    /// Stops the periodic poll for the tenant. Idempotent — calling for an unknown tenant
    /// is a no-op. Any in-flight snapshot finishes naturally; no future ticks are scheduled.
    /// </summary>
    Task StopPeriodicAsync(string tenantId);

    /// <summary>
    /// Heartbeat — the host's middleware/SignalR hub calls this on each authenticated
    /// request (or every N seconds while a reporting page is open) to signal "this tenant
    /// is still actively using the app." Resets the idle-timeout clock; without it,
    /// periodic polling auto-stops after <see cref="SnapshotTriggerOptions.IdleTimeout"/>.
    /// </summary>
    void Heartbeat(string tenantId);

    /// <summary>UTC of the most recent successful snapshot for the tenant; null if none yet.</summary>
    DateTimeOffset? GetLastSnapshotAt(string tenantId);

    /// <summary>Time until the next scheduled tick for the tenant; null if not currently polled.</summary>
    TimeSpan? GetTimeUntilNextSnapshot(string tenantId);

    /// <summary>
    /// Stream of completion events the host's SignalR / <c>IPolarToastChannel</c> can
    /// subscribe to. One event per completed snapshot tick (success or failure) so the UI
    /// can update the "last refreshed at … / next in …" header in real time.
    /// </summary>
    IAsyncEnumerable<SnapshotCompletedEvent> CompletedEventsAsync(CancellationToken ct);
}

/// <summary>
/// Event emitted on each completed snapshot tick (success or failure). Streamed via
/// <see cref="IReportSnapshotTrigger.CompletedEventsAsync"/>.
/// </summary>
public sealed record SnapshotCompletedEvent
{
    /// <summary>Tenant whose snapshot just completed.</summary>
    public required string TenantId { get; init; }

    /// <summary>UTC when the snapshot tick finished.</summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>The free-form reason passed to <see cref="IReportSnapshotTrigger.TriggerImmediateAsync"/>, or "Periodic" for scheduled ticks.</summary>
    public required string Reason { get; init; }

    /// <summary>True if every resource ingested without error; false otherwise.</summary>
    public required bool Success { get; init; }

    /// <summary>Number of resources that had non-zero deltas this tick.</summary>
    public required int ResourcesIngested { get; init; }

    /// <summary>Total rows upserted across all resources this tick.</summary>
    public required int RowsIngested { get; init; }

    /// <summary>Wall-clock duration of the tick from start to publish.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Failure message when <see cref="Success"/> is false; null otherwise.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Configuration for <see cref="IReportSnapshotTrigger"/>. Bound from
/// <c>PolarSharp:Reporting:SnapshotTrigger</c>.
/// </summary>
public sealed class SnapshotTriggerOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "PolarSharp:Reporting:SnapshotTrigger";

    /// <summary>Default per-tenant polling cadence when <see cref="IReportSnapshotTrigger.StartPeriodicAsync"/>
    /// is called without an explicit interval. Default 15 minutes.</summary>
    public TimeSpan DefaultInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Stop periodic polling for a tenant if no <see cref="IReportSnapshotTrigger.Heartbeat"/>
    /// has been received in this window. Default 30 minutes — covers a closed-laptop
    /// scenario without burning API quota for an absent user.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Minimum interval between manual <c>TriggerImmediateAsync(reason:"ManualRefresh")</c>
    /// calls per tenant. Excess calls within the cooldown return the most-recent completion
    /// event without firing a new snapshot. Default 30 seconds.</summary>
    public TimeSpan ManualRefreshDebounce { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Bounded capacity of the completion-event channel. When full, the oldest
    /// event is dropped (DropOldest). 100 keeps memory bounded while giving slow consumers
    /// generous slack. Default 100.</summary>
    public int CompletionChannelCapacity { get; set; } = 100;
}
