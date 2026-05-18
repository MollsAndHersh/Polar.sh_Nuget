namespace PolarSharp.MultiTenant.Notifications;

/// <summary>
/// Configuration for the opt-in tenant lifecycle notification dispatcher.
/// </summary>
/// <remarks>
/// <para>
/// Bound from the <c>PolarSharp:MultiTenant:Notifications</c> configuration section
/// (<see cref="SectionName"/>) by <c>AddPolarMultiTenantNotifications</c>.
/// </para>
/// <para>
/// The package itself is opt-in at two levels: the host has to install this NuGet package
/// AND set <see cref="Enabled"/> to <see langword="true"/>. When the package is installed
/// but the flag is left at the default <see langword="false"/>, the registered
/// <see cref="MediatR.INotificationHandler{TNotification}"/> still runs but immediately
/// returns — no validation, no template rendering, no outbound HTTP. This makes it safe to
/// include the package in a base image and switch it on later via config.
/// </para>
/// </remarks>
public sealed class TenantNotificationOptions
{
    /// <summary>The configuration section name bound by <c>AddPolarMultiTenantNotifications</c>.</summary>
    public const string SectionName = "PolarSharp:MultiTenant:Notifications";

    /// <summary>
    /// Gets or sets the master enable flag. When <see langword="false"/> (default), the
    /// package's <see cref="MediatR.INotificationHandler{TNotification}"/> is registered but
    /// does nothing. Set <see langword="true"/> to activate the dispatcher.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the channels enabled for tenant lifecycle notifications.</summary>
    public TenantNotificationChannels EnabledChannels { get; set; } = new();

    /// <summary>Gets or sets the email channel configuration.</summary>
    public EmailChannelOptions Email { get; set; } = new();

    /// <summary>Gets or sets the SMS channel configuration.</summary>
    public SmsChannelOptions Sms { get; set; } = new();

    /// <summary>Gets or sets the webhook channel configuration (for custom integrations).</summary>
    public WebhookChannelOptions Webhook { get; set; } = new();

    /// <summary>
    /// Gets or sets the templates per status transition. Defaults provided; hosts can
    /// override any individual transition.
    /// </summary>
    public TenantNotificationTemplates Templates { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether email is sent to site managers whose
    /// email address has not been verified. Default <see langword="false"/> — unverified
    /// recipients are skipped (the SMS + webhook channels are unaffected).
    /// </summary>
    public bool SendToUnverifiedEmail { get; set; }
}

/// <summary>Per-channel enable flags.</summary>
public sealed class TenantNotificationChannels
{
    /// <summary>Gets or sets a value indicating whether the email channel is enabled. Default <see langword="true"/>.</summary>
    public bool Email { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the SMS channel is enabled. Default <see langword="false"/>.</summary>
    public bool Sms { get; set; }

    /// <summary>Gets or sets a value indicating whether the webhook channel is enabled. Default <see langword="false"/>.</summary>
    public bool Webhook { get; set; }
}

/// <summary>Email channel configuration.</summary>
public sealed class EmailChannelOptions
{
    /// <summary>Gets or sets the email provider. Only SendGrid is supported in v1.0.</summary>
    public EmailProvider Provider { get; set; } = EmailProvider.SendGrid;

    /// <summary>Gets or sets the sender email address (the From: header on outgoing messages).</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender display name.</summary>
    public string FromDisplayName { get; set; } = "PolarSharp Platform";

    /// <summary>Gets or sets the SendGrid provider configuration.</summary>
    public SendGridOptions SendGrid { get; set; } = new();
}

/// <summary>Supported email providers.</summary>
public enum EmailProvider
{
    /// <summary>SendGrid v3 Mail Send API.</summary>
    SendGrid = 0,
}

/// <summary>SendGrid-specific configuration.</summary>
public sealed class SendGridOptions
{
    /// <summary>
    /// Gets or sets the name of the environment variable holding the SendGrid API key.
    /// The key itself is never read from appsettings to avoid accidental commits.
    /// </summary>
    public string ApiKeyEnvVar { get; set; } = "SENDGRID_API_KEY";
}

/// <summary>SMS channel configuration.</summary>
public sealed class SmsChannelOptions
{
    /// <summary>Gets or sets the SMS provider. Only Twilio is supported in v1.0.</summary>
    public SmsProvider Provider { get; set; } = SmsProvider.Twilio;

    /// <summary>Gets or sets the Twilio provider configuration.</summary>
    public TwilioOptions Twilio { get; set; } = new();
}

/// <summary>Supported SMS providers.</summary>
public enum SmsProvider
{
    /// <summary>Twilio Programmable SMS Messages API.</summary>
    Twilio = 0,
}

/// <summary>Twilio-specific configuration.</summary>
public sealed class TwilioOptions
{
    /// <summary>Gets or sets the name of the environment variable holding the Twilio Account SID.</summary>
    public string AccountSidEnvVar { get; set; } = "TWILIO_ACCOUNT_SID";

    /// <summary>Gets or sets the name of the environment variable holding the Twilio Auth Token.</summary>
    public string AuthTokenEnvVar { get; set; } = "TWILIO_AUTH_TOKEN";

    /// <summary>Gets or sets the Twilio-provisioned From: phone number in E.164 format (e.g., <c>+15558675309</c>).</summary>
    public string FromNumber { get; set; } = string.Empty;
}

/// <summary>Webhook channel configuration.</summary>
public sealed class WebhookChannelOptions
{
    /// <summary>Gets or sets the HTTPS URL the webhook POSTs to with a JSON payload.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the environment variable holding the HMAC signing secret.
    /// The secret is used to compute the <c>X-PolarSharp-Signature</c> header.
    /// </summary>
    public string SigningSecretEnvVar { get; set; } = "POLARSHARP_WEBHOOK_SECRET";

    /// <summary>Gets or sets the timeout (in seconds) for the webhook POST. Default 10. Range [1, 300].</summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Templates per status transition. Each template carries the email subject + body and
/// the SMS body; placeholder substitution renders the message at dispatch time.
/// </summary>
/// <remarks>
/// <para>
/// Supported placeholders:
/// <list type="bullet">
///   <item><c>{TenantName}</c> — the tenant's display name (falls back to identifier if null).</item>
///   <item><c>{TenantIdentifier}</c> — the tenant's Finbuckle identifier slug.</item>
///   <item><c>{NewStatus}</c> — the new status (e.g., <c>Suspended</c>).</item>
///   <item><c>{PreviousStatus}</c> — the status before the change.</item>
///   <item><c>{Reason}</c> — the human-readable reason supplied by the caller.</item>
///   <item><c>{OccurredAt}</c> — UTC timestamp of the change (ISO 8601).</item>
/// </list>
/// </para>
/// </remarks>
public sealed class TenantNotificationTemplates
{
    /// <summary>Gets or sets the template for the Active->Suspended transition.</summary>
    public NotificationTemplate Suspended { get; set; } = new()
    {
        EmailSubject = "Your {TenantName} account has been suspended",
        EmailBody = "Hello,\n\nYour account ({TenantName}) was suspended on {OccurredAt}. Reason: {Reason}.\n\nTo appeal, contact your platform administrator.\n\n-- PolarSharp Platform",
        SmsBody = "{TenantName}: account suspended ({Reason}). Contact admin to appeal.",
    };

    /// <summary>Gets or sets the template for the Suspended/Inactive->Active transition (reactivation).</summary>
    public NotificationTemplate Reactivated { get; set; } = new()
    {
        EmailSubject = "Your {TenantName} account has been reactivated",
        EmailBody = "Hello,\n\nYour account ({TenantName}) was reactivated on {OccurredAt}. Service is fully restored.\n\n-- PolarSharp Platform",
        SmsBody = "{TenantName}: account reactivated. Service restored.",
    };

    /// <summary>Gets or sets the template for the Active->Inactive transition.</summary>
    public NotificationTemplate Deactivated { get; set; } = new()
    {
        EmailSubject = "Your {TenantName} account has been deactivated",
        EmailBody = "Hello,\n\nYour account ({TenantName}) was deactivated on {OccurredAt}. Reason: {Reason}.\n\nYour data is preserved; reactivation is possible by contacting your platform administrator.\n\n-- PolarSharp Platform",
        SmsBody = "{TenantName}: account deactivated ({Reason}). Data preserved.",
    };

    /// <summary>Gets or sets the template for the any->Deleted transition (soft-delete).</summary>
    public NotificationTemplate Deleted { get; set; } = new()
    {
        EmailSubject = "Your {TenantName} account has been scheduled for deletion",
        EmailBody = "Hello,\n\nYour account ({TenantName}) was scheduled for deletion on {OccurredAt}. Reason: {Reason}.\n\nYour data will be permanently removed after the retention period. Reactivation is still possible during this window -- contact your platform administrator.\n\n-- PolarSharp Platform",
        SmsBody = "{TenantName}: account scheduled for deletion. Contact admin during retention to reactivate.",
    };
}

/// <summary>A single notification template (email subject + email body + SMS body).</summary>
/// <remarks>
/// All three fields support placeholder substitution; see
/// <see cref="TenantNotificationTemplates"/> for the supported placeholder list.
/// </remarks>
public sealed class NotificationTemplate
{
    /// <summary>Gets or sets the email subject line. Required when the email channel is enabled.</summary>
    public string EmailSubject { get; set; } = string.Empty;

    /// <summary>Gets or sets the email body (plain text). Required when the email channel is enabled.</summary>
    public string EmailBody { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMS body. Required when the SMS channel is enabled. Keep under 160 chars.</summary>
    public string SmsBody { get; set; } = string.Empty;
}
