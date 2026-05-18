using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

/// <summary>
/// MediatR notification handler that bridges <see cref="TenantStatusChangedNotification"/>
/// events from the tenant-lifecycle infrastructure into
/// <see cref="LitestreamRegenCoordinator"/> exclusion-set mutations.
/// </summary>
/// <remarks>
/// <para>
/// On Suspended / Inactive / Deleted: the tenant ID is added to the coordinator's exclusion
/// set, causing the next regeneration to omit that tenant's <c>.db</c> file from the
/// <c>dbs:</c> array — Litestream stops replicating it on the next config reload.
/// </para>
/// <para>
/// On Active (typically a reactivation): the tenant ID is removed from the exclusion set,
/// causing the next regeneration to re-include it for replication.
/// </para>
/// <para>
/// Per the Stage C design decision, existing cloud snapshots are PRESERVED (subject to
/// <see cref="LitestreamOptions.RetentionDays"/>) when a tenant is excluded — only new WAL
/// replication stops. This is intentional so suspended tenants remain restorable from the
/// last full snapshot if/when they are reactivated.
/// </para>
/// <para>
/// The handler is a no-op when either <see cref="LitestreamOptions.UseLitestream"/> or
/// <see cref="LitestreamOptions.AutoRegenerateOnTenantChange"/> is <see langword="false"/>,
/// so registration is always safe.
/// </para>
/// </remarks>
public sealed class LitestreamTenantLifecycleHandler : INotificationHandler<TenantStatusChangedNotification>
{
    private readonly LitestreamRegenCoordinator _coordinator;
    private readonly IOptionsMonitor<LitestreamOptions> _options;
    private readonly ILogger<LitestreamTenantLifecycleHandler> _logger;

    /// <summary>Initializes a new <see cref="LitestreamTenantLifecycleHandler"/>.</summary>
    /// <param name="coordinator">The shared regen coordinator holding the exclusion set.</param>
    /// <param name="options">Live Litestream options snapshot used to gate handler activity.</param>
    /// <param name="logger">Logger.</param>
    public LitestreamTenantLifecycleHandler(
        LitestreamRegenCoordinator coordinator,
        IOptionsMonitor<LitestreamOptions> options,
        ILogger<LitestreamTenantLifecycleHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _coordinator = coordinator;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task Handle(TenantStatusChangedNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var opts = _options.CurrentValue;
        if (!opts.UseLitestream || !opts.AutoRegenerateOnTenantChange)
        {
            _logger.LogDebug(
                "Litestream lifecycle handler: ignoring tenant {TenantId} status change " +
                "({Previous} -> {New}) because Litestream auto-regeneration is disabled " +
                "(UseLitestream={Use}, AutoRegenerate={Auto}).",
                notification.TenantId,
                notification.PreviousStatus,
                notification.NewStatus,
                opts.UseLitestream,
                opts.AutoRegenerateOnTenantChange);
            return Task.CompletedTask;
        }

        if (notification.NewStatus == TenantStatus.Active)
        {
            var removed = _coordinator.RemoveExclusion(notification.TenantId);
            _logger.LogInformation(
                "Litestream lifecycle handler: tenant {TenantId} reactivated ({Previous} -> Active); " +
                "exclusion {Action}.",
                notification.TenantId,
                notification.PreviousStatus,
                removed ? "removed" : "was not present");
        }
        else
        {
            var transitionDescription = string.Format(CultureInfo.InvariantCulture,
                "{0} -> {1}: {2}",
                notification.PreviousStatus,
                notification.NewStatus,
                notification.Reason);
            var added = _coordinator.AddExclusion(notification.TenantId, transitionDescription);
            _logger.LogInformation(
                "Litestream lifecycle handler: tenant {TenantId} status change ({Transition}); " +
                "exclusion {Action}.",
                notification.TenantId,
                transitionDescription,
                added ? "added" : "was already present");
        }

        return Task.CompletedTask;
    }
}
