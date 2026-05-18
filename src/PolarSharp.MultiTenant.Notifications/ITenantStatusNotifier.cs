using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.Notifications;

/// <summary>
/// Dispatches templated lifecycle notifications to the tenant's site manager across one or
/// more channels (email, SMS, webhook).
/// </summary>
/// <remarks>
/// <para>
/// Implementations are registered as singletons and are exercised by the package's
/// <see cref="MediatR.INotificationHandler{TNotification}"/> for
/// <see cref="TenantStatusChangedNotification"/>. Hosts that need to fire an out-of-band
/// notification (without going through <c>ITenantStatusService</c>) can resolve this
/// interface from DI and call it directly.
/// </para>
/// </remarks>
public interface ITenantStatusNotifier
{
    /// <summary>
    /// Resolves the template for the transition, renders it with the notification's data, and
    /// fans out to every enabled channel in parallel. Individual channel failures are logged
    /// and isolated — they do not abort the other channels and do not propagate to the caller.
    /// </summary>
    /// <param name="notification">The MediatR notification carrying the status-change snapshot.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyAsync(TenantStatusChangedNotification notification, CancellationToken ct);
}
