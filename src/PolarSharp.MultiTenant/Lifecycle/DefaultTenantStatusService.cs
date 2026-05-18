using Finbuckle.MultiTenant.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.Lifecycle;

/// <summary>
/// Default <see cref="ITenantStatusService"/> implementation backed by Finbuckle's
/// <see cref="IMultiTenantStore{TTenantInfo}"/> for tenant lookup and persistence, and
/// <see cref="IMediator"/> for publishing <see cref="TenantStatusChangedNotification"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Ordering:</strong> the store write is committed BEFORE the notification is
/// published. Downstream subscribers therefore observe the new state when they query the
/// store from inside their handler. If notification dispatch fails, the state change is
/// already persisted and the failure is logged at Error level — the operation still reports
/// <see cref="TenantStatusChangeResult.Success"/> <c>true</c>.
/// </para>
/// <para>
/// <strong>Idempotency:</strong> if the tenant is already in the requested status, the
/// service returns <see cref="TenantStatusChangeResult.WasIdempotentNoOp"/> <c>true</c>,
/// does NOT touch the store, and does NOT publish a notification.
/// </para>
/// </remarks>
public sealed class DefaultTenantStatusService : ITenantStatusService
{
    private readonly IMultiTenantStore<PolarTenantInfo> _store;
    private readonly IMediator _mediator;
    private readonly ILogger<DefaultTenantStatusService> _logger;
    private readonly IOptionsMonitor<TenantStatusServiceOptions> _options;
    private readonly TimeProvider _clock;

    /// <summary>Initializes a new <see cref="DefaultTenantStatusService"/>.</summary>
    /// <param name="store">The Finbuckle-backed tenant store.</param>
    /// <param name="mediator">The MediatR mediator used to publish lifecycle notifications.</param>
    /// <param name="logger">Logger for notification-dispatch errors and audit traces.</param>
    /// <param name="options">Live options snapshot for the lifecycle policy.</param>
    /// <param name="clock">Time provider for <c>OccurredAt</c> timestamps; injectable for tests.</param>
    public DefaultTenantStatusService(
        IMultiTenantStore<PolarTenantInfo> store,
        IMediator mediator,
        ILogger<DefaultTenantStatusService> logger,
        IOptionsMonitor<TenantStatusServiceOptions> options,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _mediator = mediator;
        _logger = logger;
        _options = options;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<TenantStatusChangeResult> SuspendAsync(Guid tenantId, string reason, Guid? actorUserId = null, CancellationToken ct = default)
        => ChangeStatusAsync(tenantId, TenantStatus.Suspended, reason, actorUserId, ct);

    /// <inheritdoc/>
    public Task<TenantStatusChangeResult> ReactivateAsync(Guid tenantId, Guid? actorUserId = null, CancellationToken ct = default)
        => ChangeStatusAsync(tenantId, TenantStatus.Active, reason: "Reactivated", actorUserId, ct);

    /// <inheritdoc/>
    public Task<TenantStatusChangeResult> DeactivateAsync(Guid tenantId, string reason, Guid? actorUserId = null, CancellationToken ct = default)
        => ChangeStatusAsync(tenantId, TenantStatus.Inactive, reason, actorUserId, ct);

    /// <inheritdoc/>
    public Task<TenantStatusChangeResult> DeleteAsync(Guid tenantId, string reason, Guid? actorUserId = null, CancellationToken ct = default)
        => ChangeStatusAsync(tenantId, TenantStatus.Deleted, reason, actorUserId, ct);

    private async Task<TenantStatusChangeResult> ChangeStatusAsync(
        Guid tenantId,
        TenantStatus newStatus,
        string reason,
        Guid? actorUserId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        ct.ThrowIfCancellationRequested();

        var now = _clock.GetUtcNow();
        var tenant = await _store.GetAsync(tenantId.ToString()).ConfigureAwait(false);
        if (tenant is null)
        {
            return new TenantStatusChangeResult
            {
                Success = false,
                PreviousStatus = TenantStatus.Active,
                NewStatus = TenantStatus.Active,
                WasIdempotentNoOp = false,
                OccurredAt = now,
                FailureReason = $"Tenant '{tenantId}' was not found in the registry.",
            };
        }

        var previousStatus = tenant.Status;

        if (previousStatus == newStatus)
        {
            return new TenantStatusChangeResult
            {
                Success = true,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                WasIdempotentNoOp = true,
                OccurredAt = now,
            };
        }

        // Pre-flight: suspending an unverified tenant is gated by policy.
        if (newStatus == TenantStatus.Suspended)
        {
            var opts = _options.CurrentValue;
            if (opts.RequireVerifiedEmailForSuspension
                && !tenant.SiteManagerEmailVerified
                && !opts.SuspendUnverifiedTenantsAnyway)
            {
                return new TenantStatusChangeResult
                {
                    Success = false,
                    PreviousStatus = previousStatus,
                    NewStatus = previousStatus,
                    WasIdempotentNoOp = false,
                    OccurredAt = now,
                    FailureReason = "Site manager email is unverified; suspension would deliver unverifiable notification.",
                };
            }
        }

        tenant.Status = newStatus;

        bool stored;
        try
        {
            stored = await _store.UpdateAsync(tenant).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenant {TenantId} status change to {NewStatus} failed during store write.", tenantId, newStatus);
            // Revert the in-memory mutation so a caller that holds the same reference isn't misled.
            tenant.Status = previousStatus;
            return new TenantStatusChangeResult
            {
                Success = false,
                PreviousStatus = previousStatus,
                NewStatus = previousStatus,
                WasIdempotentNoOp = false,
                OccurredAt = now,
                FailureReason = $"Store write failed: {ex.Message}",
            };
        }

        if (!stored)
        {
            tenant.Status = previousStatus;
            return new TenantStatusChangeResult
            {
                Success = false,
                PreviousStatus = previousStatus,
                NewStatus = previousStatus,
                WasIdempotentNoOp = false,
                OccurredAt = now,
                FailureReason = "Tenant store rejected the update (concurrent modification or missing row).",
            };
        }

        var notification = new TenantStatusChangedNotification
        {
            TenantId = tenantId,
            TenantIdentifier = tenant.Identifier,
            TenantName = tenant.Name,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Reason = reason,
            ActorUserId = actorUserId,
            OccurredAt = now,
            SiteManagerEmail = tenant.SiteManagerEmail,
            SiteManagerEmailVerified = tenant.SiteManagerEmailVerified,
            SiteManagerPhone = tenant.SiteManagerPhone,
        };

        try
        {
            await _mediator.Publish(notification, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Per contract: state change persisted; only notification dispatch failed.
            // Log Error and report Success=true so callers retry the state change idempotently
            // rather than re-applying the (already-applied) update.
            _logger.LogError(
                ex,
                "Tenant {TenantId} status changed from {PreviousStatus} to {NewStatus} but MediatR notification dispatch failed.",
                tenantId, previousStatus, newStatus);
        }

        return new TenantStatusChangeResult
        {
            Success = true,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            WasIdempotentNoOp = false,
            OccurredAt = now,
        };
    }
}
