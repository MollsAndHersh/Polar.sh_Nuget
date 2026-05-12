using System.Collections.Immutable;
using System.Text;
using Microsoft.Extensions.Localization;

namespace PolarSharp.Webhooks.Toast;

/// <summary>
/// A rich, strongly-typed Polar webhook event payload formatted for UI toast display.
/// </summary>
/// <remarks>
/// <para>
/// Carry this record through to the UI layer and call <see cref="Localize"/> at render
/// time — <em>not</em> at webhook dispatch time. Webhooks arrive in a background thread
/// where <c>CultureInfo.CurrentUICulture</c> has no request-scoped culture. The UI layer
/// (Blazor component, SignalR hub, SSE endpoint) has the correct culture set from the
/// request context.
/// </para>
/// <para>
/// The pre-rendered <see cref="Title"/> and <see cref="Message"/> fields always contain
/// an en-US fallback so that apps that never call <see cref="Localize"/> still work correctly.
/// </para>
/// </remarks>
public sealed record PolarToastNotification
{
    // ── Core display ─────────────────────────────────────────────────────

    /// <summary>Gets the Polar event type string, e.g. <c>"order.created"</c>.</summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Gets the pre-rendered display title (en-US fallback).
    /// </summary>
    /// <value>
    /// Call <see cref="Localize"/> to get a culture-correct copy of this record with the
    /// title rendered in the user's locale.
    /// </value>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the pre-rendered display message (en-US fallback).
    /// </summary>
    /// <value>
    /// Call <see cref="Localize"/> to get a culture-correct copy of this record with the
    /// message rendered in the user's locale.
    /// </value>
    public required string Message { get; init; }

    /// <summary>Gets the visual severity of this notification.</summary>
    public required ToastSeverity Severity { get; init; }

    /// <summary>Gets the suggested display duration.</summary>
    public required TimeSpan Duration { get; init; }

    // ── Event identity ────────────────────────────────────────────────────

    /// <summary>
    /// Gets the UTC timestamp of the Polar event (from the webhook payload, not delivery time).
    /// </summary>
    public required DateTimeOffset EventTimestamp { get; init; }

    /// <summary>
    /// Gets Polar's webhook delivery ID.
    /// </summary>
    /// <value>
    /// Use as an idempotency key — Polar delivers webhooks at-least-once, so the same
    /// <see cref="WebhookId"/> may be written to the channel more than once.
    /// </value>
    public required string WebhookId { get; init; }

    // ── Order context ─────────────────────────────────────────────────────

    /// <summary>Gets the Polar order ID (present on <c>order.*</c> and <c>checkout.*</c> events).</summary>
    public Option<string> OrderId { get; init; }

    /// <summary>Gets the human-readable order reference number.</summary>
    public Option<string> OrderNumber { get; init; }

    /// <summary>Gets the order channel: <c>"web"</c>, <c>"api"</c>, <c>"embed"</c>, etc.</summary>
    public Option<string> Channel { get; init; }

    /// <summary>Gets the order status at event time.</summary>
    public Option<string> OrderStatus { get; init; }

    /// <summary>Gets the line items on the order.</summary>
    /// <value>Empty when the event carries no item detail.</value>
    public IReadOnlyList<PolarToastLineItem> Items { get; init; } = [];

    // ── Customer context ─────────────────────────────────────────────────

    /// <summary>Gets the Polar customer ID.</summary>
    public Option<string> CustomerId { get; init; }

    /// <summary>Gets the customer email address.</summary>
    public Option<string> CustomerEmail { get; init; }

    /// <summary>Gets the customer display name.</summary>
    public Option<string> CustomerName { get; init; }

    // ── Product / subscription context ────────────────────────────────────

    /// <summary>Gets the Polar product ID.</summary>
    public Option<string> ProductId { get; init; }

    /// <summary>Gets the product display name.</summary>
    public Option<string> ProductName { get; init; }

    /// <summary>Gets the subscription ID (present on <c>subscription.*</c> events).</summary>
    public Option<string> SubscriptionId { get; init; }

    /// <summary>Gets the subscription status at event time.</summary>
    public Option<string> SubscriptionStatus { get; init; }

    /// <summary>Gets the plan name from the subscription's price.</summary>
    public Option<string> PlanName { get; init; }

    // ── Pricing context ────────────────────────────────────────────────────

    /// <summary>Gets the order or refund total.</summary>
    public Option<PolarToastAmount> TotalAmount { get; init; }

    /// <summary>Gets the tax amount, when reported in the event payload.</summary>
    public Option<PolarToastAmount> TaxAmount { get; init; }

    // ── Error context ───────────────────────────────────────────────────────

    /// <summary>Gets error details when the event represents a failure (e.g., payment declined).</summary>
    public Option<PolarToastError> Error { get; init; }

    // ── Localization support ──────────────────────────────────────────────

    /// <summary>
    /// Gets the resource key used to look up the localized title template.
    /// </summary>
    /// <value>
    /// Format: <c>Toast_{NormalizedEventType}_Title</c>,
    /// e.g. <c>Toast_order_created_Title</c>.
    /// </value>
    public required string TitleLocalizationKey { get; init; }

    /// <summary>
    /// Gets the resource key used to look up the localized message template.
    /// </summary>
    /// <value>Format: <c>Toast_{NormalizedEventType}_MessageTemplate</c>.</value>
    public required string MessageLocalizationKey { get; init; }

    /// <summary>
    /// Gets the flat token dictionary extracted from the event payload.
    /// </summary>
    /// <value>
    /// Key-value pairs with string values (e.g., <c>{"OrderId", "ord_123"}</c>). Used by
    /// <see cref="Localize"/> to substitute <c>{TokenName}</c> placeholders in templates.
    /// </value>
    public required IReadOnlyDictionary<string, string> TokenValues { get; init; }

    // ── Extension properties ───────────────────────────────────────────────

    /// <summary>
    /// Gets additional event properties not covered by the typed fields above.
    /// </summary>
    /// <value>
    /// Populated from raw event payload fields for forward-compatibility with new Polar fields.
    /// Review each property before rendering in the UI — do not surface raw internal IDs.
    /// </value>
    public IReadOnlyDictionary<string, string> ExtendedProperties { get; init; }
        = ImmutableDictionary<string, string>.Empty;

    // ── Localize ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a copy of this notification with <see cref="Title"/> and <see cref="Message"/>
    /// re-rendered using <paramref name="localizer"/> under the caller's current UI culture.
    /// </summary>
    /// <param name="localizer">
    /// The <see cref="IStringLocalizer"/> to use for key lookup.
    /// Inject <c>IStringLocalizer&lt;PolarWebhookMessages&gt;</c> from DI, or any
    /// <c>IStringLocalizer</c> backed by your preferred resource source.
    /// </param>
    /// <returns>
    /// A new <see cref="PolarToastNotification"/> with localized <see cref="Title"/> and
    /// <see cref="Message"/>. All other properties are unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="localizer"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Call this method at render time — inside a Blazor component's lifecycle,
    /// a SignalR hub method, or an SSE endpoint handler — where
    /// <c>CultureInfo.CurrentUICulture</c> is correctly set from the request.
    /// <para>
    /// Resolution order for each string (most specific wins):
    /// <list type="number">
    ///   <item><description>Host app's <see cref="IStringLocalizer"/> custom implementation.</description></item>
    ///   <item><description>PolarSharp built-in .resx (en-US and es-MX shipped complete).</description></item>
    ///   <item><description>Pre-rendered <see cref="Title"/>/<see cref="Message"/> fallback (always en-US).</description></item>
    /// </list>
    /// </para>
    /// <example>
    /// <code>
    /// // In a Blazor component:
    /// @inject IPolarLocalizer Localizer
    ///
    /// await foreach (var raw in PolarToasts.Reader.ReadAllAsync(_cts.Token))
    /// {
    ///     var localized = raw.Localize(Localizer);
    ///     ToastService.Show(localized.Title, localized.Message);
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public PolarToastNotification Localize(IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var localizedTitle   = RenderTemplate(localizer[TitleLocalizationKey].Value,   TokenValues)
                               ?? Title;
        var localizedMessage = RenderTemplate(localizer[MessageLocalizationKey].Value, TokenValues)
                               ?? Message;

        return this with { Title = localizedTitle, Message = localizedMessage };
    }

    private static string RenderTemplate(string? template, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template)) return template ?? string.Empty;
        var sb = new StringBuilder(template);
        foreach (var (key, value) in tokens)
            sb.Replace($"{{{key}}}", value);
        return sb.ToString();
    }
}

/// <summary>
/// A line item on a Polar order, carried in <see cref="PolarToastNotification.Items"/>.
/// </summary>
/// <param name="ProductId">The Polar product ID.</param>
/// <param name="ProductName">The product display name.</param>
/// <param name="Quantity">The number of units.</param>
/// <param name="UnitPrice">The price per unit, if available.</param>
/// <param name="LineTotal">The total for this line item, if available.</param>
public sealed record PolarToastLineItem(
    string ProductId,
    string ProductName,
    int Quantity,
    Option<PolarToastAmount> UnitPrice,
    Option<PolarToastAmount> LineTotal);

/// <summary>
/// A monetary amount with ISO 4217 currency code.
/// </summary>
/// <param name="Amount">The numeric amount.</param>
/// <param name="Currency">The ISO 4217 currency code (e.g., <c>"USD"</c>).</param>
/// <param name="Formatted">
/// An optional pre-formatted display string (e.g., <c>"$29.99"</c>) populated when
/// Polar includes a formatted value in the event payload.
/// </param>
public sealed record PolarToastAmount(
    decimal Amount,
    string Currency,
    Option<string> Formatted);

/// <summary>
/// Error details carried on failure events (payment declined, subscription lapse, etc.).
/// </summary>
/// <param name="Code">A machine-readable error code.</param>
/// <param name="Message">A human-readable error message.</param>
/// <param name="Detail">Additional detail about the error, when available.</param>
public sealed record PolarToastError(
    string Code,
    string Message,
    Option<string> Detail);
