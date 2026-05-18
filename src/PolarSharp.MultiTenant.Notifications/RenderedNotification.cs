using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.Notifications;

/// <summary>
/// A single resolved + rendered notification ready for dispatch on one or more channels.
/// </summary>
/// <remarks>
/// Built once per <see cref="TenantStatusChangedNotification"/> by the dispatcher and passed
/// to each channel. Carries both the source notification (for channels like Webhook that
/// want the full payload) and the pre-rendered text surfaces (for SendGrid + Twilio that
/// only consume rendered text).
/// </remarks>
public sealed record RenderedNotification
{
    /// <summary>Gets the source notification this rendering was built from.</summary>
    public required TenantStatusChangedNotification Source { get; init; }

    /// <summary>Gets the rendered email subject (placeholders substituted).</summary>
    public required string EmailSubject { get; init; }

    /// <summary>Gets the rendered email body (placeholders substituted).</summary>
    public required string EmailBody { get; init; }

    /// <summary>Gets the rendered SMS body (placeholders substituted).</summary>
    public required string SmsBody { get; init; }
}
