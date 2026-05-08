using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using PolarSharp.Concurrency;

namespace PolarSharp.Telemetry;

/// <summary>
/// Provides all PolarSharp metrics via a named <see cref="Meter"/>.
/// Integrates with OpenTelemetry, Prometheus, Azure Monitor, and any other
/// <see cref="IMeterFactory"/>-compatible backend with zero configuration.
/// </summary>
/// <remarks>
/// Zero overhead when no <see cref="MeterListener"/> is attached (AOT-safe).
/// All metrics are tagged per-tenant and per-resource where applicable.
/// </remarks>
internal sealed class PolarMeter : IDisposable
{
    /// <summary>Gets the name of the <see cref="Meter"/>.</summary>
    public const string MeterName = "PolarSharp";

    private static readonly string Version =
        typeof(PolarMeter).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

    private readonly Meter _meter;

    // Outbound API call metrics
    private readonly Counter<long> _requests;
    private readonly Counter<long> _errors;
    private readonly Histogram<double> _requestDuration;
    private readonly UpDownCounter<long> _inflightGauge;

    // Webhook metrics
    private readonly Counter<long> _webhooksReceived;
    private readonly Counter<long> _webhookVerificationFailures;
    private readonly Counter<long> _webhooksRejectedInvalidSignature;
    private readonly Counter<long> _webhooksRejectedRateLimited;
    private readonly Counter<long> _webhooksRejectedTooLarge;
    private readonly Counter<long> _webhooksRejectedIpNotAllowed;
    private readonly ObservableGauge<int> _suspiciousActivityGauge;

    private int _suspiciousActivity;
    private readonly Dictionary<string, Func<int>> _channelDepthProviders = new();

    /// <summary>Gets the inflight tracker backed by the <c>polar.requests.inflight</c> gauge.</summary>
    public InflightTracker InflightTracker { get; }

    /// <summary>
    /// Initializes the <see cref="PolarMeter"/> using the provided <see cref="IMeterFactory"/>.
    /// </summary>
    /// <param name="meterFactory">The factory to create the <see cref="Meter"/> from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="meterFactory"/> is <see langword="null"/>.</exception>
    public PolarMeter(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _meter = meterFactory.Create(MeterName, Version);

        _requests = _meter.CreateCounter<long>(
            "polar.requests",
            description: "Number of outbound Polar API requests, tagged by resource, operation, and HTTP status.");

        _errors = _meter.CreateCounter<long>(
            "polar.errors",
            description: "Number of non-retryable Polar API errors.");

        _requestDuration = _meter.CreateHistogram<double>(
            "polar.request.duration",
            unit: "ms",
            description: "Duration of outbound Polar API calls in milliseconds.");

        _inflightGauge = _meter.CreateUpDownCounter<long>(
            "polar.requests.inflight",
            description: "Number of currently in-flight Polar API requests.");

        _webhooksReceived = _meter.CreateCounter<long>(
            "polar.webhooks.received",
            description: "Number of Polar webhook deliveries received.");

        _webhookVerificationFailures = _meter.CreateCounter<long>(
            "polar.webhooks.verification_failures",
            description: "Number of webhook HMAC signature verification failures.");

        _webhooksRejectedInvalidSignature = _meter.CreateCounter<long>(
            "polar.webhooks.rejected_invalid_signature",
            description: "Webhook deliveries rejected due to invalid HMAC signature.");

        _webhooksRejectedRateLimited = _meter.CreateCounter<long>(
            "polar.webhooks.rejected_rate_limited",
            description: "Webhook deliveries rejected by rate limiter.");

        _webhooksRejectedTooLarge = _meter.CreateCounter<long>(
            "polar.webhooks.rejected_payload_too_large",
            description: "Webhook deliveries rejected due to oversized payload.");

        _webhooksRejectedIpNotAllowed = _meter.CreateCounter<long>(
            "polar.webhooks.rejected_ip_not_allowed",
            description: "Webhook deliveries rejected because the source IP is not allowlisted.");

        _suspiciousActivityGauge = _meter.CreateObservableGauge<int>(
            "polar.webhooks.suspicious_activity",
            () => _suspiciousActivity,
            description: "Set to 1 when an anomalous webhook verification failure rate is detected; 0 when clear.");

        _meter.CreateObservableGauge<int>(
            "polar.channel.depth",
            () => _channelDepthProviders.Select(kvp =>
                new Measurement<int>(kvp.Value(), new KeyValuePair<string, object?>("polar.channel_name", kvp.Key))),
            description: "Current depth (number of queued items) per named PolarSharp internal channel.");

        InflightTracker = new InflightTracker(_inflightGauge);
    }

    /// <summary>Records a completed outbound Polar API request.</summary>
    /// <param name="resource">Resource area (e.g., <c>"orders"</c>).</param>
    /// <param name="operation">Operation name (e.g., <c>"get"</c>).</param>
    /// <param name="statusCode">HTTP response status code.</param>
    /// <param name="tenantId">Tenant identifier (empty for single-tenant).</param>
    /// <param name="durationMs">Duration of the call in milliseconds.</param>
    public void RecordRequest(string resource, string operation, int statusCode, string tenantId, double durationMs)
    {
        var tags = new TagList
        {
            { "polar.resource", resource },
            { "polar.operation", operation },
            { "http.status_code", statusCode },
            { "polar.tenant_id", tenantId }
        };
        _requests.Add(1, tags);
        _requestDuration.Record(durationMs, tags);
    }

    /// <summary>Records a non-retryable Polar API error.</summary>
    /// <param name="resource">Resource area.</param>
    /// <param name="errorType">The discriminating type name of the <see cref="PolarError"/>.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    public void RecordError(string resource, string errorType, string tenantId) =>
        _errors.Add(1, new TagList
        {
            { "polar.resource", resource },
            { "polar.error_type", errorType },
            { "polar.tenant_id", tenantId }
        });

    /// <summary>Records a received webhook delivery.</summary>
    /// <param name="eventType">Polar event type string (e.g., <c>"order.created"</c>).</param>
    /// <param name="tenantId">Tenant identifier.</param>
    public void RecordWebhookReceived(string eventType, string tenantId = "") =>
        _webhooksReceived.Add(1, new TagList
        {
            { "polar.event_type", eventType },
            { "polar.tenant_id", tenantId }
        });

    /// <summary>Records a webhook HMAC verification failure.</summary>
    /// <param name="sourceIpHash">SHA-256 hash of the source IP (never the raw IP — GDPR).</param>
    public void RecordVerificationFailure(string sourceIpHash) =>
        _webhookVerificationFailures.Add(1, new TagList { { "polar.source_ip_hash", sourceIpHash } });

    /// <summary>Records a webhook rejected for invalid HMAC signature.</summary>
    /// <param name="sourceIpHash">SHA-256 hash of the source IP.</param>
    public void IncrementWebhookRejectedInvalidSignature(string sourceIpHash) =>
        _webhooksRejectedInvalidSignature.Add(1, new TagList { { "polar.source_ip_hash", sourceIpHash } });

    /// <summary>Records a webhook rejected by the rate limiter.</summary>
    /// <param name="sourceIpHash">SHA-256 hash of the source IP.</param>
    public void IncrementWebhookRateLimited(string sourceIpHash) =>
        _webhooksRejectedRateLimited.Add(1, new TagList { { "polar.source_ip_hash", sourceIpHash } });

    /// <summary>Records a webhook rejected due to payload being too large.</summary>
    public void IncrementWebhookRejectedTooLarge() => _webhooksRejectedTooLarge.Add(1);

    /// <summary>Records a webhook rejected because the source IP was not in the allowlist.</summary>
    /// <param name="sourceIpHash">SHA-256 hash of the source IP.</param>
    public void IncrementWebhookRejectedIpNotAllowed(string sourceIpHash) =>
        _webhooksRejectedIpNotAllowed.Add(1, new TagList { { "polar.source_ip_hash", sourceIpHash } });

    /// <summary>Sets the suspicious-activity gauge to 1 (attack signal detected).</summary>
    public void SignalSuspiciousActivity() => Volatile.Write(ref _suspiciousActivity, 1);

    /// <summary>Clears the suspicious-activity gauge back to 0.</summary>
    public void ClearSuspiciousActivity() => Volatile.Write(ref _suspiciousActivity, 0);

    /// <summary>
    /// Registers a channel depth provider for the <c>polar.channel.depth</c> observable gauge.
    /// Call once per channel during service initialization.
    /// </summary>
    /// <param name="channelName">Short name identifying the channel (e.g., <c>"toast"</c>, <c>"webhook-queue"</c>).</param>
    /// <param name="depthProvider">A function that returns the current depth of the channel.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/> or empty.</exception>
    public void RegisterChannelDepthProvider(string channelName, Func<int> depthProvider)
    {
        ArgumentNullException.ThrowIfNull(channelName);
        ArgumentNullException.ThrowIfNull(depthProvider);
        _channelDepthProviders[channelName] = depthProvider;
    }

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}
