namespace PolarSharp.MultiTenant.Notifications;

/// <summary>
/// Thrown by a notification channel when delivery to its third-party API fails (non-2xx
/// HTTP response, timeout, signing error, etc.).
/// </summary>
/// <remarks>
/// Channel-level delivery failures are non-fatal. The
/// <see cref="TenantStatusChangedNotificationHandler"/> catches this exception, logs it at
/// Error level, and continues — a failed email send does NOT prevent SMS or webhook channels
/// from running, and notification dispatch failure is never reported back through MediatR to
/// the originating <c>ITenantStatusService</c> call (the tenant status change has already
/// been persisted at that point).
/// </remarks>
public sealed class TenantNotificationDeliveryException : Exception
{
    /// <summary>Initializes a new <see cref="TenantNotificationDeliveryException"/>.</summary>
    /// <param name="channelName">Human-readable channel name (e.g., <c>SendGrid</c>, <c>Twilio</c>, <c>Webhook</c>).</param>
    /// <param name="message">Description of the failure.</param>
    public TenantNotificationDeliveryException(string channelName, string message)
        : base($"[{channelName}] {message}")
    {
        ChannelName = channelName;
    }

    /// <summary>Initializes a new <see cref="TenantNotificationDeliveryException"/> with an inner exception.</summary>
    /// <param name="channelName">Human-readable channel name.</param>
    /// <param name="message">Description of the failure.</param>
    /// <param name="innerException">The underlying transport or serialization exception.</param>
    public TenantNotificationDeliveryException(string channelName, string message, Exception innerException)
        : base($"[{channelName}] {message}", innerException)
    {
        ChannelName = channelName;
    }

    /// <summary>Initializes a new <see cref="TenantNotificationDeliveryException"/> for an HTTP failure.</summary>
    /// <param name="channelName">Human-readable channel name.</param>
    /// <param name="statusCode">The HTTP status code returned by the third-party API.</param>
    /// <param name="responseBody">The response body (truncated to a sensible length upstream).</param>
    public TenantNotificationDeliveryException(string channelName, int statusCode, string responseBody)
        : base($"[{channelName}] HTTP {statusCode}: {responseBody}")
    {
        ChannelName = channelName;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>Gets the human-readable channel name that produced the failure.</summary>
    public string ChannelName { get; }

    /// <summary>Gets the HTTP status code, when the failure was a non-2xx HTTP response. <see langword="null"/> for non-HTTP failures.</summary>
    public int? StatusCode { get; }

    /// <summary>Gets the response body from the failed HTTP request, when applicable. <see langword="null"/> for non-HTTP failures.</summary>
    public string? ResponseBody { get; }
}
