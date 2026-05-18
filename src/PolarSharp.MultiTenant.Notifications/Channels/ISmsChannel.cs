namespace PolarSharp.MultiTenant.Notifications.Channels;

/// <summary>Abstraction over an SMS provider used by the dispatcher.</summary>
/// <remarks>
/// Implementations are registered as singletons and consume <see cref="System.Net.Http.IHttpClientFactory"/>
/// for outbound HTTP. v1.0 ships <see cref="TwilioSmsChannel"/>; future versions may add
/// AWS SNS or other E.164-capable providers.
/// </remarks>
public interface ISmsChannel
{
    /// <summary>Sends the rendered SMS body to the supplied recipient.</summary>
    /// <param name="rendered">The rendered notification with body already substituted.</param>
    /// <param name="toNumber">Recipient phone number in E.164 format (e.g., <c>+15558675309</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="TenantNotificationDeliveryException">Thrown on non-2xx HTTP response or transport failure.</exception>
    Task SendAsync(RenderedNotification rendered, string toNumber, CancellationToken ct);
}
