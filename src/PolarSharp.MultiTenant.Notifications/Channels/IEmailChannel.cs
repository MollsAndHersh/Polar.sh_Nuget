namespace PolarSharp.MultiTenant.Notifications.Channels;

/// <summary>Abstraction over a transactional email provider used by the dispatcher.</summary>
/// <remarks>
/// Implementations are registered as singletons and consume <see cref="System.Net.Http.IHttpClientFactory"/>
/// for outbound HTTP. v1.0 ships <see cref="SendGridEmailChannel"/>; future versions may add
/// MailKit, Azure Communication Services, or AWS SES variants.
/// </remarks>
public interface IEmailChannel
{
    /// <summary>Sends the rendered email to the supplied recipient.</summary>
    /// <param name="rendered">The rendered notification with subject + body already substituted.</param>
    /// <param name="toAddress">Recipient email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="TenantNotificationDeliveryException">Thrown on non-2xx HTTP response or transport failure.</exception>
    Task SendAsync(RenderedNotification rendered, string toAddress, CancellationToken ct);
}
