using System.Security.Claims;
using System.Threading.Channels;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant;
using PolarSharp.Reporting.Identity;
using PolarSharp.Reporting.Snapshot;

namespace PolarSharp.Reporting.Identity.Tests;

/// <summary>
/// V20-005 Phase 3 unit tests for the per-request heartbeat middleware. Cover the four
/// behaviors the middleware must guarantee: heartbeat fires for authenticated +
/// tenant-resolved requests; no-ops for anonymous requests; no-ops when no tenant is
/// resolved; failures inside the trigger don't break the request.
/// </summary>
public sealed class PolarSnapshotHeartbeatMiddlewareTests
{
    private const string TenantA = "tenant-a";

    [Fact]
    public async Task Authenticated_request_with_tenant_calls_Heartbeat()
    {
        var trigger = new RecordingTrigger();
        var middleware = new PolarSnapshotHeartbeatMiddleware(_ => Task.CompletedTask, NullLogger<PolarSnapshotHeartbeatMiddleware>.Instance);

        var ctx = BuildAuthenticatedContext();
        await middleware.InvokeAsync(ctx, trigger, BuildAccessor(TenantA));

        Assert.Equal(1, trigger.HeartbeatCount);
        Assert.Equal(TenantA, trigger.LastHeartbeatTenantId);
    }

    [Fact]
    public async Task Anonymous_request_does_NOT_call_Heartbeat()
    {
        var trigger = new RecordingTrigger();
        var middleware = new PolarSnapshotHeartbeatMiddleware(_ => Task.CompletedTask, NullLogger<PolarSnapshotHeartbeatMiddleware>.Instance);

        var ctx = new DefaultHttpContext();   // unauthenticated by default
        await middleware.InvokeAsync(ctx, trigger, BuildAccessor(TenantA));

        Assert.Equal(0, trigger.HeartbeatCount);
    }

    [Fact]
    public async Task Authenticated_request_with_no_tenant_does_NOT_call_Heartbeat()
    {
        var trigger = new RecordingTrigger();
        var middleware = new PolarSnapshotHeartbeatMiddleware(_ => Task.CompletedTask, NullLogger<PolarSnapshotHeartbeatMiddleware>.Instance);

        var ctx = BuildAuthenticatedContext();
        await middleware.InvokeAsync(ctx, trigger, BuildAccessor(tenantId: null));

        Assert.Equal(0, trigger.HeartbeatCount);
    }

    [Fact]
    public async Task Heartbeat_throwing_does_NOT_block_the_request()
    {
        var trigger = new ThrowingTrigger();
        var nextCalled = false;
        var middleware = new PolarSnapshotHeartbeatMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<PolarSnapshotHeartbeatMiddleware>.Instance);

        var ctx = BuildAuthenticatedContext();
        await middleware.InvokeAsync(ctx, trigger, BuildAccessor(TenantA));

        Assert.True(nextCalled, "Request must continue even when Heartbeat throws.");
    }

    [Fact]
    public async Task InvokeAsync_always_calls_next_delegate()
    {
        var trigger = new RecordingTrigger();
        var nextCalled = false;
        var middleware = new PolarSnapshotHeartbeatMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<PolarSnapshotHeartbeatMiddleware>.Instance);

        var ctx = new DefaultHttpContext();   // anonymous
        await middleware.InvokeAsync(ctx, trigger, BuildAccessor(TenantA));

        Assert.True(nextCalled, "Next delegate must run regardless of heartbeat path.");
    }

    // ── Test doubles ────────────────────────────────────────────────────────────

    private static DefaultHttpContext BuildAuthenticatedContext()
    {
        var ctx = new DefaultHttpContext();
        var identity = new ClaimsIdentity(authenticationType: "test");
        identity.AddClaim(new Claim(ClaimTypes.Name, "alice"));
        ctx.User = new ClaimsPrincipal(identity);
        return ctx;
    }

    private static IMultiTenantContextAccessor<PolarTenantInfo> BuildAccessor(string? tenantId) =>
        new StubAccessor(tenantId);

    private sealed class StubAccessor(string? tenantId) : IMultiTenantContextAccessor<PolarTenantInfo>
    {
        public IMultiTenantContext<PolarTenantInfo> MultiTenantContext { get; set; } =
            new MultiTenantContext<PolarTenantInfo>(
                tenantId is null
                    ? new PolarTenantInfo()                                                                  // empty id ⇒ middleware sees "no tenant"
                    : new PolarTenantInfo { Id = tenantId, Identifier = tenantId, Name = tenantId });

        IMultiTenantContext IMultiTenantContextAccessor.MultiTenantContext => MultiTenantContext;
    }

    private sealed class RecordingTrigger : IReportSnapshotTrigger
    {
        public int HeartbeatCount { get; private set; }
        public string? LastHeartbeatTenantId { get; private set; }
        public void Heartbeat(string tenantId) { HeartbeatCount++; LastHeartbeatTenantId = tenantId; }
        public Task<SnapshotCompletedEvent> TriggerImmediateAsync(string tenantId, string reason, CancellationToken ct = default) => throw new NotImplementedException();
        public Task StartPeriodicAsync(string tenantId, TimeSpan interval) => throw new NotImplementedException();
        public Task StopPeriodicAsync(string tenantId) => throw new NotImplementedException();
        public DateTimeOffset? GetLastSnapshotAt(string tenantId) => null;
        public TimeSpan? GetTimeUntilNextSnapshot(string tenantId) => null;
        public IAsyncEnumerable<SnapshotCompletedEvent> CompletedEventsAsync(CancellationToken ct) => Channel.CreateBounded<SnapshotCompletedEvent>(1).Reader.ReadAllAsync(ct);
    }

    private sealed class ThrowingTrigger : IReportSnapshotTrigger
    {
        public void Heartbeat(string tenantId) => throw new InvalidOperationException("simulated heartbeat failure");
        public Task<SnapshotCompletedEvent> TriggerImmediateAsync(string tenantId, string reason, CancellationToken ct = default) => throw new NotImplementedException();
        public Task StartPeriodicAsync(string tenantId, TimeSpan interval) => throw new NotImplementedException();
        public Task StopPeriodicAsync(string tenantId) => throw new NotImplementedException();
        public DateTimeOffset? GetLastSnapshotAt(string tenantId) => null;
        public TimeSpan? GetTimeUntilNextSnapshot(string tenantId) => null;
        public IAsyncEnumerable<SnapshotCompletedEvent> CompletedEventsAsync(CancellationToken ct) => Channel.CreateBounded<SnapshotCompletedEvent>(1).Reader.ReadAllAsync(ct);
    }
}
