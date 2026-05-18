namespace PolarSharp.MultiTenant.Lifecycle;

/// <summary>
/// Canonical service for changing tenant lifecycle status. All callers — SaaS admin
/// APIs, tenant self-closure flows, automatic enforcement handlers — should go through
/// this service. Each operation updates the tenant entity AND publishes the
/// corresponding MediatR notification atomically, so downstream subscribers
/// (notifications, Litestream reconfiguration, audit logging, etc.) react consistently.
/// </summary>
/// <remarks>
/// <para>
/// Mutating <see cref="PolarTenantInfo.Status"/> directly is supported but bypasses the
/// notification pipeline — only do so when you specifically do NOT want downstream
/// subscribers to fire (e.g., bulk seeding, integration test fixtures).
/// </para>
/// </remarks>
public interface ITenantStatusService
{
    /// <summary>
    /// Suspends the tenant (status -> <see cref="TenantStatus.Suspended"/>). Idempotent: suspending
    /// an already-suspended tenant is a no-op (does not refire the notification).
    /// </summary>
    /// <param name="tenantId">The tenant to suspend.</param>
    /// <param name="reason">Human-readable reason (surfaced in the notification template; logged in the audit trail).</param>
    /// <param name="actorUserId">Optional ID of the user/system actor performing the suspension. Used for audit logging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TenantStatusChangeResult"/> indicating outcome.</returns>
    Task<TenantStatusChangeResult> SuspendAsync(Guid tenantId, string reason, Guid? actorUserId = null, CancellationToken ct = default);

    /// <summary>Reactivates a tenant (status -> <see cref="TenantStatus.Active"/> from any non-Active state). Idempotent.</summary>
    /// <param name="tenantId">The tenant to reactivate.</param>
    /// <param name="actorUserId">Optional ID of the user/system actor performing the reactivation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TenantStatusChangeResult"/> indicating outcome.</returns>
    Task<TenantStatusChangeResult> ReactivateAsync(Guid tenantId, Guid? actorUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Deactivates the tenant (status -> <see cref="TenantStatus.Inactive"/>). Use for tenant-initiated
    /// closures or long-term disablement. Reversible via <see cref="ReactivateAsync"/>. Idempotent.
    /// </summary>
    /// <param name="tenantId">The tenant to deactivate.</param>
    /// <param name="reason">Human-readable reason (surfaced in the notification template; logged in the audit trail).</param>
    /// <param name="actorUserId">Optional ID of the user/system actor performing the deactivation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TenantStatusChangeResult"/> indicating outcome.</returns>
    Task<TenantStatusChangeResult> DeactivateAsync(Guid tenantId, string reason, Guid? actorUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes the tenant (status -> <see cref="TenantStatus.Deleted"/>). Tenant data is preserved
    /// for the configured retention period; permanent removal occurs after retention expires.
    /// Reversible by AppMasterAdmin during the retention window.
    /// </summary>
    /// <param name="tenantId">The tenant to soft-delete.</param>
    /// <param name="reason">Human-readable reason (surfaced in the notification template; logged in the audit trail).</param>
    /// <param name="actorUserId">Optional ID of the user/system actor performing the deletion.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TenantStatusChangeResult"/> indicating outcome.</returns>
    Task<TenantStatusChangeResult> DeleteAsync(Guid tenantId, string reason, Guid? actorUserId = null, CancellationToken ct = default);
}
