using System.Text.Json.Serialization;
using PolarSharp.Webhooks.Serialization;

namespace PolarSharp.Webhooks.Events;

/// <summary>
/// Abstract base for all strongly-typed Polar webhook event records.
/// Deserialized from the HTTP POST body via the Standard Webhooks spec.
/// </summary>
/// <remarks>
/// <para>
/// Polar delivers events as at-least-once. The same <see cref="WebhookId"/> may
/// arrive more than once (e.g., after a timeout). Every handler implementation
/// MUST be idempotent — use <see cref="WebhookId"/> as the deduplication key.
/// </para>
/// <para>
/// The concrete type is resolved by <see cref="WebhookEventJsonConverter"/> based on
/// the <see cref="Type"/> discriminator property.
/// </para>
/// </remarks>
[JsonConverter(typeof(WebhookEventJsonConverter))]
public abstract record WebhookEvent
{
    /// <summary>Gets the Polar event type string (e.g., <c>"order.created"</c>).</summary>
    /// <value>Lower-case dot-separated string matching Polar's event type enumeration.</value>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Gets the webhook delivery identifier from the <c>webhook-id</c> header.</summary>
    /// <value>Stable across retries for the same logical delivery. Use for idempotency.</value>
    /// <remarks>
    /// Per the Standard Webhooks spec, the delivery ID is carried in the <c>webhook-id</c>
    /// HTTP header, NOT in the JSON body. <see cref="WebhookValidator"/> injects this value
    /// after HMAC verification via a <c>with</c> expression.
    /// <para>
    /// <c>[JsonIgnore]</c> prevents STJ from looking for this property in the JSON body.
    /// <c>required</c> is intentionally omitted here — STJ in .NET 10 rejects the combination
    /// of <c>[JsonIgnore]</c> and <c>required</c> on <c>init</c>-only record properties.
    /// The contract is enforced by <see cref="WebhookValidator"/>, which always injects
    /// both <see cref="WebhookId"/> and <see cref="Timestamp"/> before returning a result.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public string WebhookId { get; init; } = string.Empty;

    /// <summary>Gets the timestamp parsed from the <c>webhook-timestamp</c> header.</summary>
    /// <remarks>
    /// Per the Standard Webhooks spec, the event timestamp is carried in the
    /// <c>webhook-timestamp</c> HTTP header, NOT in the JSON body. <see cref="WebhookValidator"/>
    /// injects this value after HMAC verification via a <c>with</c> expression.
    /// <para>
    /// <c>[JsonIgnore]</c> prevents STJ from looking for this property in the JSON body.
    /// See <see cref="WebhookId"/> remarks for why <c>required</c> is omitted.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.MinValue;

    /// <summary>Gets the Polar organization ID pre-parsed from the payload before HMAC verification.</summary>
    /// <remarks>
    /// Used for multi-tenant webhook routing: the <see cref="WebhookValidator"/> extracts
    /// <c>data.organization_id</c> from the raw bytes before (and independently of) signature
    /// verification, solely to select the correct per-tenant HMAC secret. The verified
    /// value is then injected here via a <c>with</c> expression after successful verification.
    /// <para>
    /// <c>[JsonIgnore]</c> prevents STJ from looking for this property in the JSON body
    /// (the actual field is on the concrete data sub-object, e.g. <c>WebhookOrderData.OrganizationId</c>).
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public string? OrganizationId { get; init; }

    /// <summary>Gets the resolved tenant identifier mapped from <see cref="OrganizationId"/>.</summary>
    /// <remarks>
    /// Populated by <see cref="IWebhookTenantResolver"/> after HMAC verification succeeds.
    /// Handlers can inject <c>IMultiTenantContext&lt;PolarTenantInfo&gt;</c> to get full tenant
    /// details; this property provides the raw tenant ID string for scenarios where only the
    /// identifier is needed without resolving the full tenant object.
    /// <para><c>[JsonIgnore]</c> — not present in the JSON payload.</para>
    /// </remarks>
    [JsonIgnore]
    public string? ResolvedTenantId { get; init; }
}
