using PolarSharp.ValueObjects;

namespace PolarSharp.Webhooks;

/// <summary>
/// Configuration options for the Polar webhook receiver.
/// </summary>
/// <remarks>
/// Bound from the <c>PolarSharp:Webhooks</c> section of <c>appsettings.json</c>.
/// <para>
/// Supports zero-downtime secret rotation by accepting a list of secrets in
/// <see cref="Secrets"/>. The webhook endpoint verifies against all configured
/// secrets and accepts the request if any matches. During rotation, keep both the
/// old and new secrets active until Polar has fully switched to the new one.
/// </para>
/// <example>
/// Single-secret configuration:
/// <code>
/// "PolarSharp": {
///   "Webhooks": {
///     "Secret": "whsec_xxx",
///     "Path": "/hooks/polar"
///   }
/// }
/// </code>
/// Multi-secret rotation configuration:
/// <code>
/// "PolarSharp": {
///   "Webhooks": {
///     "Secrets": ["whsec_new_xxx", "whsec_old_xxx"],
///     "Path": "/hooks/polar"
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public class PolarWebhookOptions
{
    /// <summary>
    /// Gets or sets a single webhook signing secret.
    /// </summary>
    /// <value>
    /// A Base64-encoded secret, optionally prefixed with <c>whsec_</c>.
    /// Treated as a one-element list in <see cref="GetSecrets"/>. Use <see cref="Secrets"/>
    /// for zero-downtime rotation with multiple simultaneous secrets.
    /// </value>
    /// <remarks>
    /// Bound from <c>PolarSharp:Webhooks:Secret</c>. If both <c>Secret</c> and
    /// <c>Secrets</c> are set, they are combined.
    /// </remarks>
    public string? Secret { get; set; }

    /// <summary>
    /// Gets or sets the list of active webhook signing secrets.
    /// </summary>
    /// <value>
    /// Zero or more Base64-encoded secrets, each optionally prefixed with <c>whsec_</c>.
    /// A webhook is accepted if the computed HMAC matches any secret in this list.
    /// </value>
    /// <remarks>
    /// Bound from <c>PolarSharp:Webhooks:Secrets</c>. Combine with <see cref="Secret"/>
    /// for maximum compatibility during rotations.
    /// </remarks>
    public List<string> Secrets { get; set; } = [];

    /// <summary>
    /// Gets or sets the route path where the webhook endpoint is mounted.
    /// </summary>
    /// <value>
    /// A path string starting with <c>/</c>. Default: <c>/hooks/polar</c>. For production,
    /// use a randomized path segment (e.g., <c>/hooks/polar/a3f7b2c9-4d1e-...</c>) to add
    /// defense-in-depth against enumeration attacks.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:Path</c>.</remarks>
    public string Path { get; set; } = "/hooks/polar";

    /// <summary>
    /// Gets or sets a value indicating whether non-HTTPS webhook requests are rejected.
    /// </summary>
    /// <value>
    /// <see langword="true"/> (default) to return HTTP 400 for non-HTTPS requests.
    /// The endpoint returns 400 (not a redirect) because Polar's sender does not follow
    /// redirects — a redirect would cause Polar to mark the delivery as failed and retry
    /// indefinitely against the HTTP URL.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:RequireHttps</c>.</remarks>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// Gets or sets the allowed timestamp drift in seconds.
    /// </summary>
    /// <value>
    /// Webhooks whose <c>webhook-timestamp</c> header is more than this many seconds
    /// in the past or future are rejected to prevent replay attacks. Default: <c>300</c>
    /// (five minutes), matching the Standard Webhooks specification recommendation.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:ToleranceSeconds</c>.</remarks>
    public int ToleranceSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum allowed webhook request body size in bytes.
    /// </summary>
    /// <value>
    /// Requests exceeding this size return HTTP 413 before the body is read into memory.
    /// Default: <c>1,048,576</c> (1 MB). Polar payloads are typically under 50 KB in practice.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:MaxPayloadBytes</c>.</remarks>
    public int MaxPayloadBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Gets or sets a value indicating whether rate limiting is applied to the webhook endpoint.
    /// </summary>
    /// <value><see langword="true"/> (default) to apply a fixed-window rate limiter per source IP.</value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:EnableRateLimiting</c>.</remarks>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of webhook requests per IP per rate-limit window.
    /// </summary>
    /// <value>Default: <c>300</c> requests per minute.</value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:RateLimitPermitLimit</c>.</remarks>
    public int RateLimitPermitLimit { get; set; } = 300;

    /// <summary>
    /// Gets or sets the rate-limit window duration.
    /// </summary>
    /// <value>Default: <c>1 minute</c>.</value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:RateLimitWindowSeconds</c>.</remarks>
    public int RateLimitWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets a value indicating whether IP source allowlisting is enabled.
    /// </summary>
    /// <value>
    /// <see langword="false"/> (default) — opt-in because it requires maintaining Polar's
    /// current IP range list. When enabled, requests from IPs not in
    /// <see cref="AllowedSourceIpRanges"/> return HTTP 403 before body read.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:EnableIpAllowlist</c>.</remarks>
    public bool EnableIpAllowlist { get; set; }

    /// <summary>
    /// Gets or sets the CIDR ranges allowed to deliver webhooks.
    /// </summary>
    /// <value>
    /// A list of CIDR notation strings (e.g., <c>["34.x.x.x/16"]</c>). Only checked when
    /// <see cref="EnableIpAllowlist"/> is <see langword="true"/>. Consult Polar's documentation
    /// for the current list of sender IP ranges.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:AllowedSourceIpRanges</c>.</remarks>
    public List<string> AllowedSourceIpRanges { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether <c>X-Forwarded-For</c> is used instead
    /// of <c>RemoteIpAddress</c> for IP allowlist checks.
    /// </summary>
    /// <value>
    /// <see langword="false"/> (default). Set to <see langword="true"/> when the app runs
    /// behind a load balancer or reverse proxy that rewrites the remote IP.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:UseForwardedForHeader</c>.</remarks>
    public bool UseForwardedForHeader { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a warning is logged for received events
    /// that have no registered handler.
    /// </summary>
    /// <value><see langword="true"/> (default).</value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:WarnOnUnhandledEventTypes</c>.</remarks>
    public bool WarnOnUnhandledEventTypes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether application startup fails when any known
    /// Polar event type has no registered handler.
    /// </summary>
    /// <value>
    /// <see langword="false"/> (default) — missing handlers produce warnings only.
    /// Set to <see langword="true"/> in production to force explicit coverage of every event type.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:FailOnMissingHandlers</c>.</remarks>
    public bool FailOnMissingHandlers { get; set; }

    /// <summary>
    /// Gets or sets the maximum time in seconds to wait for in-flight background webhook
    /// processing to complete during graceful shutdown.
    /// </summary>
    /// <value>Default: <c>30</c> seconds.</value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:GracefulDrainTimeoutSeconds</c>.</remarks>
    public int GracefulDrainTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the toast notification configuration.
    /// </summary>
    /// <value>
    /// Configures which Polar event types generate real-time UI notification payloads
    /// on the <see cref="Toast.IPolarToastChannel"/> channel. <see langword="null"/> means
    /// toast notifications are disabled.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:ToastNotifications</c>.</remarks>
    public Toast.PolarToastOptions? ToastNotifications { get; set; }

    /// <summary>
    /// Gets or sets the webhook event reconciliation configuration.
    /// </summary>
    /// <value>
    /// When set, enables the periodic event reconciliation service that replays missed
    /// events via <c>polar.Events.ListAsync(since: lastCheckpoint)</c>. <see langword="null"/>
    /// means reconciliation is disabled (default).
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:Webhooks:Reconciliation</c>.</remarks>
    public Reconciliation.PolarReconciliationOptions? Reconciliation { get; set; }

    /// <summary>
    /// Returns all configured webhook secrets, combining <see cref="Secret"/> and
    /// <see cref="Secrets"/> into a single enumerable.
    /// </summary>
    /// <returns>
    /// All non-empty, non-whitespace secret values. The list is never <see langword="null"/>
    /// but may be empty if neither <see cref="Secret"/> nor <see cref="Secrets"/> is set
    /// (which fails startup validation).
    /// </returns>
    public IEnumerable<WebhookSecret> GetSecrets()
    {
        if (!string.IsNullOrWhiteSpace(Secret))
            yield return new WebhookSecret(Secret);

        foreach (var s in Secrets)
        {
            if (!string.IsNullOrWhiteSpace(s))
                yield return new WebhookSecret(s);
        }
    }
}
