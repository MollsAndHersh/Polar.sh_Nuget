using MediatR;
using Microsoft.Extensions.Logging;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.Notifications;

/// <summary>
/// MediatR <see cref="INotificationHandler{TNotification}"/> for
/// <see cref="TenantStatusChangedNotification"/> that bridges into <see cref="ITenantStatusNotifier"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registration of this handler is added by <c>AddPolarMultiTenantNotifications</c> via the
/// MediatR assembly scan. When the package is installed but disabled via
/// <see cref="TenantNotificationOptions.Enabled"/>, the handler still runs but the
/// underlying notifier returns immediately — no template lookup, no channel dispatch.
/// </para>
/// <para>
/// Any <see cref="TenantNotificationDeliveryException"/> raised by individual channels has
/// already been caught and logged inside <see cref="DefaultTenantStatusNotifier"/>.
/// This handler defensively catches everything else so a programming error in the dispatcher
/// can never propagate back through MediatR into the originating
/// <c>ITenantStatusService</c> call (which would mislead the caller into thinking the
/// underlying status change failed).
/// </para>
/// <para>
/// <strong>Exception handling exception (intentional):</strong>
/// <see cref="OperationCanceledException"/> is NOT swallowed — it is rethrown unchanged so
/// that host shutdown can complete cleanly. When the host is shutting down, the cancellation
/// token passed to <c>Handle</c> fires; treating the resulting OCE as a normal completion
/// would (a) make the handler report success when work was actually canceled, and (b) prevent
/// the host's graceful-shutdown path from observing the cancellation. Standard async-handler
/// practice — see Microsoft.Extensions.Hosting guidance on IHostedService cancellation semantics.
/// </para>
/// </remarks>
public sealed class TenantStatusChangedNotificationHandler : INotificationHandler<TenantStatusChangedNotification>
{
    private readonly ITenantStatusNotifier _notifier;
    private readonly ILogger<TenantStatusChangedNotificationHandler> _logger;

    /// <summary>Initializes a new <see cref="TenantStatusChangedNotificationHandler"/>.</summary>
    /// <param name="notifier">The lifecycle notification dispatcher.</param>
    /// <param name="logger">Logger for unexpected dispatcher failures.</param>
    public TenantStatusChangedNotificationHandler(
        ITenantStatusNotifier notifier,
        ILogger<TenantStatusChangedNotificationHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(logger);
        _notifier = notifier;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Handle(TenantStatusChangedNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            await _notifier.NotifyAsync(notification, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // DO NOT swallow OCE — propagate so host shutdown can observe the cancellation
            // and complete cleanly. See class XML <remarks> for rationale.
            throw;
        }
#pragma warning disable CA1031 // Other exceptions: notification dispatch must never bubble back into ITenantStatusService.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(
                ex,
                "Unexpected failure in tenant lifecycle notification dispatcher for tenant {TenantId} " +
                "({Previous} -> {New}). The status change has been persisted; only the notification " +
                "dispatch failed.",
                notification.TenantId,
                notification.PreviousStatus,
                notification.NewStatus);
        }
    }
}
