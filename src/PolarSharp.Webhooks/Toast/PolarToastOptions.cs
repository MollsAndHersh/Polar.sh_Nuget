namespace PolarSharp.Webhooks.Toast;

/// <summary>
/// Configuration for the Polar webhook real-time toast notification channel.
/// </summary>
/// <remarks>
/// Bound from <c>PolarSharp:Webhooks:ToastNotifications</c>. Only event types listed in
/// <see cref="Events"/> produce notifications on <see cref="IPolarToastChannel"/>. All
/// other event types are silently ignored by the toast subsystem — their business handlers
/// still run normally.
/// </remarks>
public class PolarToastOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether toast notifications are emitted.
    /// </summary>
    /// <value><see langword="true"/> (default when this section is present in config).</value>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of notifications that can be queued before
    /// the oldest are dropped.
    /// </summary>
    /// <value>
    /// Default: <c>100</c>. Prevents unbounded memory growth when the UI consumer
    /// is slower than the webhook delivery rate.
    /// </value>
    public int ChannelCapacity { get; set; } = 100;

    /// <summary>
    /// Gets or sets the list of event-type-specific toast configurations.
    /// </summary>
    /// <value>
    /// Each entry specifies which Polar event type generates a notification and how
    /// to render its title and message. Unlisted event types produce no toast.
    /// </value>
    public List<PolarToastEventConfig> Events { get; set; } = [];
}

/// <summary>
/// Per-event-type configuration for a Polar webhook toast notification.
/// </summary>
public class PolarToastEventConfig
{
    /// <summary>
    /// Gets or sets the Polar event type string that triggers this notification.
    /// </summary>
    /// <value>
    /// Must match Polar's event type exactly, e.g. <c>"order.created"</c>.
    /// </value>
    public string EventType { get; set; } = "";

    /// <summary>
    /// Gets or sets the notification title displayed in the UI.
    /// </summary>
    /// <value>
    /// A short, static title string. May be overridden at render time via
    /// <c>PolarToastNotification.Localize(localizer)</c>.
    /// </value>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the notification message template.
    /// </summary>
    /// <value>
    /// A template string containing <c>{TokenName}</c> placeholders that are substituted
    /// with values extracted from the event payload. Available tokens differ by event type —
    /// see the toast-notifications article for the full token reference.
    /// </value>
    public string MessageTemplate { get; set; } = "";

    /// <summary>
    /// Gets or sets the visual severity of the notification.
    /// </summary>
    /// <value>Default: <see cref="ToastSeverity.Info"/>.</value>
    public ToastSeverity Severity { get; set; } = ToastSeverity.Info;

    /// <summary>
    /// Gets or sets how long in seconds the notification is displayed before auto-dismissing.
    /// </summary>
    /// <value>Default: <c>5</c>. The host UI may impose its own minimum or maximum.</value>
    public int DurationSeconds { get; set; } = 5;
}

/// <summary>
/// Indicates the visual importance of a Polar toast notification.
/// </summary>
public enum ToastSeverity
{
    /// <summary>Positive outcome — order placed, subscription activated, etc.</summary>
    Success,

    /// <summary>Informational event — no immediate action required.</summary>
    Info,

    /// <summary>Event that may need attention — subscription canceled, refund initiated, etc.</summary>
    Warning,

    /// <summary>Error condition — payment failed, subscription lapsed, etc.</summary>
    Error,
}
