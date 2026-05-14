using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PolarSharp.MultiTenant;
using PolarSharp.Reporting.Snapshot;

namespace PolarSharp.Reporting.Identity;

/// <summary>
/// Middleware that calls <see cref="IReportSnapshotTrigger.Heartbeat"/> on every
/// authenticated request, so the orchestrator's idle-timeout doesn't stop polling for
/// users actively using the app. V20-005 Phase 3.
/// </summary>
/// <remarks>
/// <para>
/// Place AFTER <c>UseAuthentication()</c> and BEFORE <c>UseAuthorization()</c>. The
/// middleware is a no-op when <c>HttpContext.User.Identity?.IsAuthenticated</c> is false
/// (anonymous request) or when no tenant is resolved (the per-request scope hasn't been
/// hydrated yet — typical of pre-auth or static-asset requests).
/// </para>
/// <para>
/// <strong>Performance:</strong> <c>Heartbeat</c> on the orchestrator is an in-memory
/// timestamp update against a <c>ConcurrentDictionary</c> entry — sub-microsecond. The
/// middleware adds essentially zero per-request cost.
/// </para>
/// <para>
/// <strong>Failure isolation:</strong> a failure inside the heartbeat path must NEVER
/// block the request. Exceptions are logged at <c>Debug</c> and swallowed; the request
/// proceeds normally.
/// </para>
/// </remarks>
public sealed class PolarSnapshotHeartbeatMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PolarSnapshotHeartbeatMiddleware> _logger;

    /// <summary>Creates the middleware.</summary>
    public PolarSnapshotHeartbeatMiddleware(RequestDelegate next, ILogger<PolarSnapshotHeartbeatMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Invokes the middleware. Calls <c>Heartbeat</c> for the current tenant when the request is authenticated.</summary>
    public async Task InvokeAsync(
        HttpContext context,
        IReportSnapshotTrigger trigger,
        IMultiTenantContextAccessor<PolarTenantInfo> tenantAccessor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(tenantAccessor);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                var tenantInfo = tenantAccessor.MultiTenantContext?.TenantInfo as PolarTenantInfo;
                if (!string.IsNullOrEmpty(tenantInfo?.Id))
                    trigger.Heartbeat(tenantInfo.Id);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "PolarSharp.Reporting.Identity: heartbeat failed (non-fatal); request continues.");
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}
