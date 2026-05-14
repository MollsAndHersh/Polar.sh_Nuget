using Finbuckle.MultiTenant.Abstractions;
using PolarSharp.MultiTenant;
using PolarSharp.Reporting.Snapshot;

namespace PolarSnapshotTestApp.Endpoints;

/// <summary>
/// V20-005 Phase 3 test-app endpoints to drive and inspect the per-tenant snapshot
/// orchestrator manually. Tenant id is read from the same <c>X-Tenant-ID</c> header
/// the rest of the test app uses (resolved by Finbuckle's middleware).
/// </summary>
public static class SnapshotEndpoints
{
    /// <summary>Maps the test-only /test/snapshot/* endpoints.</summary>
    public static IEndpointRouteBuilder MapSnapshotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/test/snapshot").WithTags("Snapshot");

        group.MapGet("/last", (
            IReportSnapshotTrigger trigger,
            IMultiTenantContextAccessor<PolarTenantInfo> accessor) =>
        {
            var tenantId = ResolveTenantOrNull(accessor);
            if (tenantId is null) return Results.BadRequest(new { error = "No tenant resolved. Send X-Tenant-ID header." });
            return Results.Ok(new { tenantId, lastSnapshotAt = trigger.GetLastSnapshotAt(tenantId) });
        })
        .WithName("GetLastSnapshotAt")
        .WithSummary("UTC timestamp of the most recent successful snapshot for the current tenant.");

        group.MapGet("/next", (
            IReportSnapshotTrigger trigger,
            IMultiTenantContextAccessor<PolarTenantInfo> accessor) =>
        {
            var tenantId = ResolveTenantOrNull(accessor);
            if (tenantId is null) return Results.BadRequest(new { error = "No tenant resolved. Send X-Tenant-ID header." });
            return Results.Ok(new { tenantId, timeUntilNextSnapshot = trigger.GetTimeUntilNextSnapshot(tenantId) });
        })
        .WithName("GetTimeUntilNextSnapshot")
        .WithSummary("Time until the next scheduled snapshot tick (null when not currently polled).");

        group.MapPost("/trigger", async (
            string? reason,
            IReportSnapshotTrigger trigger,
            IMultiTenantContextAccessor<PolarTenantInfo> accessor,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenantOrNull(accessor);
            if (tenantId is null) return Results.BadRequest(new { error = "No tenant resolved. Send X-Tenant-ID header." });
            var evt = await trigger.TriggerImmediateAsync(tenantId, reason ?? "ManualRefresh", ct);
            return Results.Ok(evt);
        })
        .WithName("TriggerImmediateSnapshot")
        .WithSummary("Fires an immediate snapshot. Reason defaults to 'ManualRefresh' (debounced).");

        group.MapPost("/start", async (
            int? intervalSeconds,
            IReportSnapshotTrigger trigger,
            IMultiTenantContextAccessor<PolarTenantInfo> accessor) =>
        {
            var tenantId = ResolveTenantOrNull(accessor);
            if (tenantId is null) return Results.BadRequest(new { error = "No tenant resolved. Send X-Tenant-ID header." });
            var interval = TimeSpan.FromSeconds(intervalSeconds ?? 60);
            await trigger.StartPeriodicAsync(tenantId, interval);
            return Results.Ok(new { tenantId, intervalSeconds = (int)interval.TotalSeconds });
        })
        .WithName("StartPeriodicSnapshot")
        .WithSummary("Starts periodic snapshotting. Default interval 60s.");

        group.MapPost("/stop", async (
            IReportSnapshotTrigger trigger,
            IMultiTenantContextAccessor<PolarTenantInfo> accessor) =>
        {
            var tenantId = ResolveTenantOrNull(accessor);
            if (tenantId is null) return Results.BadRequest(new { error = "No tenant resolved. Send X-Tenant-ID header." });
            await trigger.StopPeriodicAsync(tenantId);
            return Results.Ok(new { tenantId, stopped = true });
        })
        .WithName("StopPeriodicSnapshot")
        .WithSummary("Stops periodic snapshotting for the current tenant.");

        group.MapPost("/heartbeat", (
            IReportSnapshotTrigger trigger,
            IMultiTenantContextAccessor<PolarTenantInfo> accessor) =>
        {
            var tenantId = ResolveTenantOrNull(accessor);
            if (tenantId is null) return Results.BadRequest(new { error = "No tenant resolved. Send X-Tenant-ID header." });
            trigger.Heartbeat(tenantId);
            return Results.Ok(new { tenantId, heartbeat = "sent" });
        })
        .WithName("HeartbeatSnapshot")
        .WithSummary("Sends a heartbeat (resets the orchestrator's idle-timeout clock).");

        return app;
    }

    private static string? ResolveTenantOrNull(IMultiTenantContextAccessor<PolarTenantInfo> accessor)
    {
        var info = accessor.MultiTenantContext?.TenantInfo;
        return string.IsNullOrEmpty(info?.Id) ? null : info.Id;
    }
}
