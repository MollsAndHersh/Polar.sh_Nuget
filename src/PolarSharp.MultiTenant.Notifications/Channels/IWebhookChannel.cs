namespace PolarSharp.MultiTenant.Notifications.Channels;

/// <summary>Abstraction over the outgoing webhook channel used by the dispatcher.</summary>
/// <remarks>
/// The webhook channel POSTs the full <see cref="PolarSharp.MultiTenant.Lifecycle.TenantStatusChangedNotification"/>
/// payload as JSON to the configured URL, with an HMAC-SHA256 signature in the
/// <c>X-PolarSharp-Signature</c> header so the receiver can verify authenticity.
/// </remarks>
public interface IWebhookChannel
{
    /// <summary>POSTs the rendered notification as JSON to the configured webhook URL.</summary>
    /// <param name="rendered">The rendered notification (used for the JSON payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="TenantNotificationDeliveryException">Thrown on non-2xx HTTP response or transport failure.</exception>
    Task PostAsync(RenderedNotification rendered, CancellationToken ct);
}
