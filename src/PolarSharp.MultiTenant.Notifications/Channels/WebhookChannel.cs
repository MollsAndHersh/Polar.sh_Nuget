using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.Lifecycle;
using PolarSharp.MultiTenant.Notifications.Serialization;

namespace PolarSharp.MultiTenant.Notifications.Channels;

/// <summary>
/// <see cref="IWebhookChannel"/> implementation that POSTs a JSON payload to a host-configured
/// HTTPS endpoint with an HMAC-SHA256 signature in the <c>X-PolarSharp-Signature</c> header.
/// </summary>
/// <remarks>
/// <para>
/// The signature is computed over the raw request body using the secret named by
/// <see cref="WebhookChannelOptions.SigningSecretEnvVar"/>. Receivers should compute the same
/// HMAC over the body they received and use a constant-time comparison to verify
/// authenticity. The header value format is <c>sha256={hex}</c>, lowercase hex.
/// </para>
/// </remarks>
public sealed class WebhookChannel : IWebhookChannel
{
    /// <summary>The named <see cref="HttpClient"/> registered for this channel.</summary>
    public const string HttpClientName = "PolarSharp.MultiTenant.Notifications.Webhook";

    /// <summary>HTTP header carrying the HMAC signature of the request body.</summary>
    public const string SignatureHeaderName = "X-PolarSharp-Signature";

    private const int MaxResponseBodyChars = 4096;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<TenantNotificationOptions> _options;
    private readonly ILogger<WebhookChannel> _logger;

    /// <summary>Initializes a new <see cref="WebhookChannel"/>.</summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="options">Live options snapshot.</param>
    /// <param name="logger">Logger for delivery diagnostics.</param>
    public WebhookChannel(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TenantNotificationOptions> options,
        ILogger<WebhookChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PostAsync(RenderedNotification rendered, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rendered);

        var opts = _options.CurrentValue;
        var signingSecret = Environment.GetEnvironmentVariable(opts.Webhook.SigningSecretEnvVar);
        if (string.IsNullOrEmpty(signingSecret))
        {
            throw new TenantNotificationDeliveryException(
                "Webhook",
                $"Webhook signing-secret environment variable '{opts.Webhook.SigningSecretEnvVar}' is not set.");
        }

        var payload = new WebhookPayload
        {
            TenantId = rendered.Source.TenantId,
            TenantIdentifier = rendered.Source.TenantIdentifier,
            TenantName = rendered.Source.TenantName,
            PreviousStatus = rendered.Source.PreviousStatus.ToString(),
            NewStatus = rendered.Source.NewStatus.ToString(),
            Reason = rendered.Source.Reason,
            ActorUserId = rendered.Source.ActorUserId,
            OccurredAt = rendered.Source.OccurredAt,
            RenderedEmailSubject = rendered.EmailSubject,
            RenderedEmailBody = rendered.EmailBody,
            RenderedSmsBody = rendered.SmsBody,
        };

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload, NotificationJsonContext.Default.WebhookPayload);
        var signature = ComputeSignature(body, signingSecret);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, opts.Webhook.Url)
        {
            Content = new ByteArrayContent(body)
            {
                Headers = { { "Content-Type", "application/json" } },
            },
        };
        request.Headers.Add(SignatureHeaderName, signature);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new TenantNotificationDeliveryException("Webhook", "Transport failure during webhook POST.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Timeout (HttpClient surfaces timeouts as TaskCanceledException without an
            // observable cancellation token).
            throw new TenantNotificationDeliveryException("Webhook", "Webhook POST timed out.", ex);
        }

        try
        {
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Webhook POST accepted for tenant {TenantId} ({Status} transition).",
                    rendered.Source.TenantId,
                    rendered.Source.NewStatus);
                return;
            }

            var responseBody = await ReadResponseBodyAsync(response, ct).ConfigureAwait(false);
            _logger.LogError(
                "Webhook POST returned HTTP {StatusCode} for tenant {TenantId}: {Body}",
                (int)response.StatusCode,
                rendered.Source.TenantId,
                responseBody);
            throw new TenantNotificationDeliveryException("Webhook", (int)response.StatusCode, responseBody);
        }
        finally
        {
            response.Dispose();
        }
    }

    private static string ComputeSignature(byte[] body, string secret)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
        byte[] hash = HMACSHA256.HashData(keyBytes, body);
        return "sha256=" + Convert.ToHexStringLower(hash);
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return text.Length > MaxResponseBodyChars
                ? string.Concat(text.AsSpan(0, MaxResponseBodyChars), "...[truncated]")
                : text;
        }
        catch (HttpRequestException)
        {
            return "(response body unreadable)";
        }
    }
}

/// <summary>JSON payload POSTed by the <see cref="WebhookChannel"/>.</summary>
/// <remarks>
/// A flattened view of <see cref="TenantStatusChangedNotification"/> plus the rendered
/// message surfaces. Statuses are serialized as strings (rather than enum integers) for
/// receiver readability.
/// </remarks>
public sealed record WebhookPayload
{
    /// <summary>The tenant identifier (GUID primary key).</summary>
    [JsonPropertyName("tenantId")]
    public required Guid TenantId { get; init; }

    /// <summary>The tenant's Finbuckle identifier (the routable slug).</summary>
    [JsonPropertyName("tenantIdentifier")]
    public required string TenantIdentifier { get; init; }

    /// <summary>The tenant's display name (may be null).</summary>
    [JsonPropertyName("tenantName")]
    public string? TenantName { get; init; }

    /// <summary>The tenant's status before the change, as the enum member name.</summary>
    [JsonPropertyName("previousStatus")]
    public required string PreviousStatus { get; init; }

    /// <summary>The tenant's status after the change, as the enum member name.</summary>
    [JsonPropertyName("newStatus")]
    public required string NewStatus { get; init; }

    /// <summary>The human-readable reason supplied by the caller.</summary>
    [JsonPropertyName("reason")]
    public required string Reason { get; init; }

    /// <summary>Optional ID of the user/system actor that performed the change.</summary>
    [JsonPropertyName("actorUserId")]
    public Guid? ActorUserId { get; init; }

    /// <summary>UTC timestamp at which the change was committed.</summary>
    [JsonPropertyName("occurredAt")]
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The rendered email subject (placeholders substituted).</summary>
    [JsonPropertyName("renderedEmailSubject")]
    public required string RenderedEmailSubject { get; init; }

    /// <summary>The rendered email body (placeholders substituted).</summary>
    [JsonPropertyName("renderedEmailBody")]
    public required string RenderedEmailBody { get; init; }

    /// <summary>The rendered SMS body (placeholders substituted).</summary>
    [JsonPropertyName("renderedSmsBody")]
    public required string RenderedSmsBody { get; init; }
}
