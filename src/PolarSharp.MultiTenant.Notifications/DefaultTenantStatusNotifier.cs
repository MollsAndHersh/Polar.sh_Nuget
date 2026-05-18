using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.Lifecycle;
using PolarSharp.MultiTenant.Notifications.Channels;

namespace PolarSharp.MultiTenant.Notifications;

/// <summary>
/// Default <see cref="ITenantStatusNotifier"/> that resolves the template for the lifecycle
/// transition, renders placeholders, and dispatches to enabled channels in parallel.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Channel isolation:</strong> each channel runs in its own <see cref="Task"/> via
/// <see cref="Task.WhenAll(Task[])"/>. A failure in one channel is logged but never aborts
/// or skips the others. The dispatcher itself never throws (channel exceptions are caught
/// and downgraded to error logs).
/// </para>
/// <para>
/// <strong>Recipient filtering:</strong> the email channel is skipped when the recipient's
/// address is unverified unless
/// <see cref="TenantNotificationOptions.SendToUnverifiedEmail"/> is true. The SMS channel is
/// skipped when the tenant has no phone number on file. The webhook channel always runs when
/// enabled (it has no per-recipient gating).
/// </para>
/// </remarks>
public sealed class DefaultTenantStatusNotifier : ITenantStatusNotifier
{
    private readonly IOptionsMonitor<TenantNotificationOptions> _options;
    private readonly IEmailChannel _emailChannel;
    private readonly ISmsChannel _smsChannel;
    private readonly IWebhookChannel _webhookChannel;
    private readonly ILogger<DefaultTenantStatusNotifier> _logger;

    /// <summary>Initializes a new <see cref="DefaultTenantStatusNotifier"/>.</summary>
    /// <param name="options">Live options snapshot.</param>
    /// <param name="emailChannel">Email channel (SendGrid in v1.0).</param>
    /// <param name="smsChannel">SMS channel (Twilio in v1.0).</param>
    /// <param name="webhookChannel">Webhook channel.</param>
    /// <param name="logger">Logger for transition-resolution + per-channel failures.</param>
    public DefaultTenantStatusNotifier(
        IOptionsMonitor<TenantNotificationOptions> options,
        IEmailChannel emailChannel,
        ISmsChannel smsChannel,
        IWebhookChannel webhookChannel,
        ILogger<DefaultTenantStatusNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(emailChannel);
        ArgumentNullException.ThrowIfNull(smsChannel);
        ArgumentNullException.ThrowIfNull(webhookChannel);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _emailChannel = emailChannel;
        _smsChannel = smsChannel;
        _webhookChannel = webhookChannel;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task NotifyAsync(TenantStatusChangedNotification notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var opts = _options.CurrentValue;

        if (!opts.Enabled)
        {
            return;
        }

        var template = ResolveTemplate(notification.PreviousStatus, notification.NewStatus, opts.Templates);
        if (template is null)
        {
            _logger.LogDebug(
                "No template configured for tenant {TenantId} transition {Previous} -> {New}; skipping notification dispatch.",
                notification.TenantId,
                notification.PreviousStatus,
                notification.NewStatus);
            return;
        }

        var rendered = Render(notification, template);
        var tasks = new List<Task>(capacity: 3);

        if (opts.EnabledChannels.Email)
        {
            tasks.Add(DispatchEmailAsync(notification, rendered, opts, ct));
        }

        if (opts.EnabledChannels.Sms)
        {
            tasks.Add(DispatchSmsAsync(notification, rendered, ct));
        }

        if (opts.EnabledChannels.Webhook)
        {
            tasks.Add(DispatchWebhookAsync(notification, rendered, ct));
        }

        if (tasks.Count == 0)
        {
            return;
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task DispatchEmailAsync(
        TenantStatusChangedNotification notification,
        RenderedNotification rendered,
        TenantNotificationOptions opts,
        CancellationToken ct)
    {
        if (!notification.SiteManagerEmailVerified && !opts.SendToUnverifiedEmail)
        {
            _logger.LogInformation(
                "Skipping email for tenant {TenantId}: site-manager email '{Email}' is unverified " +
                "and SendToUnverifiedEmail is false.",
                notification.TenantId,
                notification.SiteManagerEmail);
            return;
        }

        if (string.IsNullOrWhiteSpace(notification.SiteManagerEmail))
        {
            _logger.LogWarning(
                "Skipping email for tenant {TenantId}: SiteManagerEmail is empty.",
                notification.TenantId);
            return;
        }

        try
        {
            await _emailChannel.SendAsync(rendered, notification.SiteManagerEmail, ct).ConfigureAwait(false);
        }
        catch (TenantNotificationDeliveryException ex)
        {
            _logger.LogError(
                ex,
                "Email channel delivery failed for tenant {TenantId}.",
                notification.TenantId);
        }
    }

    private async Task DispatchSmsAsync(
        TenantStatusChangedNotification notification,
        RenderedNotification rendered,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(notification.SiteManagerPhone))
        {
            _logger.LogInformation(
                "Skipping SMS for tenant {TenantId}: no phone number on file.",
                notification.TenantId);
            return;
        }

        try
        {
            await _smsChannel.SendAsync(rendered, notification.SiteManagerPhone, ct).ConfigureAwait(false);
        }
        catch (TenantNotificationDeliveryException ex)
        {
            _logger.LogError(
                ex,
                "SMS channel delivery failed for tenant {TenantId}.",
                notification.TenantId);
        }
    }

    private async Task DispatchWebhookAsync(
        TenantStatusChangedNotification notification,
        RenderedNotification rendered,
        CancellationToken ct)
    {
        try
        {
            await _webhookChannel.PostAsync(rendered, ct).ConfigureAwait(false);
        }
        catch (TenantNotificationDeliveryException ex)
        {
            _logger.LogError(
                ex,
                "Webhook channel delivery failed for tenant {TenantId}.",
                notification.TenantId);
        }
    }

    internal static NotificationTemplate? ResolveTemplate(
        TenantStatus previous,
        TenantStatus next,
        TenantNotificationTemplates templates)
    {
        // Deletion always wins regardless of the prior status.
        if (next == TenantStatus.Deleted)
        {
            return templates.Deleted;
        }

        return (previous, next) switch
        {
            (TenantStatus.Active, TenantStatus.Suspended) => templates.Suspended,
            (TenantStatus.Suspended, TenantStatus.Active) => templates.Reactivated,
            (TenantStatus.Inactive, TenantStatus.Active) => templates.Reactivated,
            (TenantStatus.Active, TenantStatus.Inactive) => templates.Deactivated,
            _ => null,
        };
    }

    internal static RenderedNotification Render(
        TenantStatusChangedNotification source,
        NotificationTemplate template)
    {
        var name = source.TenantName ?? source.TenantIdentifier;
        var occurredAt = source.OccurredAt.ToString("u", System.Globalization.CultureInfo.InvariantCulture);

        return new RenderedNotification
        {
            Source = source,
            EmailSubject = Substitute(template.EmailSubject, source, name, occurredAt),
            EmailBody = Substitute(template.EmailBody, source, name, occurredAt),
            SmsBody = Substitute(template.SmsBody, source, name, occurredAt),
        };
    }

    private static string Substitute(
        string template,
        TenantStatusChangedNotification source,
        string tenantName,
        string occurredAt)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return template
            .Replace("{TenantName}", tenantName, StringComparison.Ordinal)
            .Replace("{TenantIdentifier}", source.TenantIdentifier, StringComparison.Ordinal)
            .Replace("{NewStatus}", source.NewStatus.ToString(), StringComparison.Ordinal)
            .Replace("{PreviousStatus}", source.PreviousStatus.ToString(), StringComparison.Ordinal)
            .Replace("{Reason}", source.Reason, StringComparison.Ordinal)
            .Replace("{OccurredAt}", occurredAt, StringComparison.Ordinal);
    }
}
