using MediatR;

namespace PolarSharp.MultiTenant.Lifecycle;

/// <summary>
/// MediatR notification published whenever a tenant's lifecycle status changes via
/// <see cref="ITenantStatusService"/>. Subscribed handlers can include: notification
/// dispatchers (email/SMS to site manager), Litestream auto-regenerators (excluding/
/// re-including the tenant's .db file from replication), audit log writers, etc.
/// </summary>
/// <remarks>
/// <para>
/// Published only when the status actually changes — idempotent no-ops (e.g., suspending an
/// already-suspended tenant) do NOT fire the notification. Subscribers can therefore treat
/// every notification they receive as a genuine state transition.
/// </para>
/// <para>
/// The notification carries a full snapshot of the relevant tenant fields at the time of the
/// change, so handlers do not need to re-query the store. The site-manager contact triple
/// (<see cref="SiteManagerEmail"/>, <see cref="SiteManagerEmailVerified"/>,
/// <see cref="SiteManagerPhone"/>) is included so notification dispatchers can render and
/// deliver messages without an additional round trip.
/// </para>
/// </remarks>
public sealed record TenantStatusChangedNotification : INotification
{
    /// <summary>Gets the tenant identifier (GUID primary key).</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Gets the tenant's Finbuckle identifier (the routable slug — e.g., <c>acme-corp</c>).</summary>
    public required string TenantIdentifier { get; init; }

    /// <summary>Gets the tenant's human-readable display name, if set.</summary>
    public required string? TenantName { get; init; }

    /// <summary>Gets the tenant's status before the change.</summary>
    public required TenantStatus PreviousStatus { get; init; }

    /// <summary>Gets the tenant's status after the change.</summary>
    public required TenantStatus NewStatus { get; init; }

    /// <summary>Gets the human-readable reason supplied by the caller (surfaced in templates and audit logs).</summary>
    public required string Reason { get; init; }

    /// <summary>Gets the optional ID of the user/system actor that performed the change.</summary>
    public required Guid? ActorUserId { get; init; }

    /// <summary>Gets the UTC timestamp at which the change was committed to the store.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Gets the site manager's email address (the target for lifecycle email notifications).</summary>
    public required string SiteManagerEmail { get; init; }

    /// <summary>Gets a value indicating whether the site manager email has been verified.</summary>
    public required bool SiteManagerEmailVerified { get; init; }

    /// <summary>Gets the optional E.164-format SMS phone number for the site manager.</summary>
    public required string? SiteManagerPhone { get; init; }
}
