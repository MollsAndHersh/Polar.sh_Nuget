using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Serialization;

namespace PolarSharp.Webhooks;

/// <summary>
/// Verifies Polar webhook HMAC-SHA256 signatures per the Standard Webhooks specification
/// and deserializes the payload into a strongly-typed <see cref="WebhookEvent"/>.
/// </summary>
/// <remarks>
/// <para>
/// All verification failures return a <see cref="WebhookVerificationError"/> via the
/// <see cref="Result{TValue,TError}"/> return type — never throw exceptions for 4xx-level
/// failures.
/// </para>
/// <para>
/// Signature comparison is constant-time via <see cref="CryptographicOperations.FixedTimeEquals"/>
/// to prevent timing oracle attacks.
/// </para>
/// <para>
/// All verification failure modes return the <strong>same opaque error message</strong>
/// externally, even though specific failure reason is logged internally — prevents
/// timing/information oracle attacks.
/// </para>
/// </remarks>
public sealed class WebhookValidator(
    IOptionsMonitor<PolarWebhookOptions> options,
    ILogger<WebhookValidator> logger)
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        MaxDepth = 32,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        PropertyNameCaseInsensitive = true,
    };

    private const string OpaqueError = "Webhook verification failed.";

    /// <summary>
    /// Verifies the webhook headers and body, then deserializes the event payload.
    /// </summary>
    /// <param name="webhookId">Value of the <c>webhook-id</c> header.</param>
    /// <param name="webhookTimestamp">Value of the <c>webhook-timestamp</c> header.</param>
    /// <param name="webhookSignature">Value of the <c>webhook-signature</c> header (may be comma-separated for multi-secret rotation).</param>
    /// <param name="body">The raw HTTP request body bytes.</param>
    /// <returns>
    /// A <see cref="Result{TValue,TError}"/> containing the deserialized <see cref="WebhookEvent"/>
    /// on success, or a <see cref="WebhookVerificationError"/> on any failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is <see langword="null"/>.</exception>
    public Result<WebhookEvent, WebhookVerificationError> Verify(
        string webhookId,
        string webhookTimestamp,
        string webhookSignature,
        ReadOnlySpan<byte> body)
    {
        ArgumentNullException.ThrowIfNull(webhookId);
        ArgumentNullException.ThrowIfNull(webhookTimestamp);
        ArgumentNullException.ThrowIfNull(webhookSignature);

        var opts = options.CurrentValue;

        // 1. Validate timestamp — prevent replay attacks.
        if (!TryParseTimestamp(webhookTimestamp, out var eventTime))
        {
            logger.LogWarning(
                "Polar webhook {WebhookId}: timestamp '{Timestamp}' could not be parsed as a Unix epoch integer.",
                webhookId, webhookTimestamp);
            return Result<WebhookEvent, WebhookVerificationError>.Failure(new WebhookVerificationError(OpaqueError));
        }

        var age = DateTimeOffset.UtcNow - eventTime;
        var tolerance = TimeSpan.FromSeconds(opts.ToleranceSeconds);
        var isTimestampValid = age >= -tolerance && age <= tolerance;

        // 2. Compute HMAC — always computed even when timestamp is invalid (timing uniformity).
        var signaturePayload = Encoding.UTF8.GetBytes($"{webhookId}.{webhookTimestamp}.");
        var totalLength = signaturePayload.Length + body.Length;
        var payloadBytes = ArrayPool<byte>.Shared.Rent(totalLength);

        bool verified;
        try
        {
            signaturePayload.CopyTo(payloadBytes, 0);
            body.CopyTo(payloadBytes.AsSpan(signaturePayload.Length));

            var candidateSignatures = webhookSignature
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            verified = false;
            foreach (var secret in opts.GetSecrets())
            {
                var secretBytes = Convert.FromBase64String(
                    secret.Value.StartsWith("whsec_", StringComparison.Ordinal)
                        ? secret.Value[6..]
                        : secret.Value);

                var computed = HMACSHA256.HashData(secretBytes, payloadBytes.AsSpan(0, totalLength));
                var computedB64 = Convert.ToBase64String(computed);

                foreach (var candidate in candidateSignatures)
                {
                    var candidateValue = candidate.StartsWith("v1,", StringComparison.Ordinal)
                        ? candidate[3..]
                        : candidate;

                    if (!CryptographicOperations.FixedTimeEquals(
                            Encoding.UTF8.GetBytes(computedB64),
                            Encoding.UTF8.GetBytes(candidateValue)))
                        continue;

                    verified = true;
                    break;
                }
                if (verified) break;
            }

            if (!isTimestampValid)
            {
                logger.LogWarning(
                    "Polar webhook {WebhookId}: timestamp is outside the ±{ToleranceSeconds}s window (age: {Age:g}).",
                    webhookId, opts.ToleranceSeconds, age);
                return Result<WebhookEvent, WebhookVerificationError>.Failure(new WebhookVerificationError(OpaqueError));
            }

            if (!verified)
            {
                logger.LogWarning(
                    "Polar webhook {WebhookId}: HMAC signature verification failed.",
                    webhookId);
                return Result<WebhookEvent, WebhookVerificationError>.Failure(new WebhookVerificationError(OpaqueError));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(payloadBytes, clearArray: true);
        }

        // 3. Deserialize event payload.
        try
        {
            var bodyText = Encoding.UTF8.GetString(body);
            var evt = JsonSerializer.Deserialize<WebhookEvent>(bodyText, DeserializeOptions);
            if (evt is null)
                return Result<WebhookEvent, WebhookVerificationError>.Failure(new WebhookVerificationError(OpaqueError));

            // Inject delivery metadata from headers (not in JSON body per Standard Webhooks spec).
            evt = evt with { WebhookId = webhookId, Timestamp = eventTime };
            return Result<WebhookEvent, WebhookVerificationError>.Success(evt);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Polar webhook {WebhookId}: JSON deserialization failed.", webhookId);
            return Result<WebhookEvent, WebhookVerificationError>.Failure(new WebhookVerificationError(OpaqueError));
        }
    }

    /// <summary>
    /// Deserializes a webhook payload JSON string into a typed <see cref="WebhookEvent"/>
    /// without performing HMAC signature verification.
    /// </summary>
    /// <remarks>
    /// Used internally by the reconciliation service, which trusts event data retrieved
    /// directly from the Polar API (not incoming HTTP requests). Never use this on
    /// externally-sourced data — always verify via <see cref="Verify"/> for incoming webhooks.
    /// </remarks>
    /// <param name="payloadJson">The raw JSON webhook event payload.</param>
    /// <param name="webhookId">The webhook delivery ID to inject as metadata.</param>
    /// <param name="timestamp">The event timestamp to inject as metadata.</param>
    /// <returns>
    /// A <see cref="Result{TValue,TError}"/> containing the deserialized event, or a
    /// <see cref="WebhookVerificationError"/> if deserialization fails.
    /// </returns>
    internal Result<WebhookEvent, WebhookVerificationError> Deserialize(
        string payloadJson,
        string webhookId,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);
        ArgumentNullException.ThrowIfNull(webhookId);

        try
        {
            var evt = JsonSerializer.Deserialize<WebhookEvent>(payloadJson, DeserializeOptions);
            if (evt is null)
                return Result<WebhookEvent, WebhookVerificationError>.Failure(
                    new WebhookVerificationError("Event deserialization produced null."));

            return Result<WebhookEvent, WebhookVerificationError>.Success(
                evt with { WebhookId = webhookId, Timestamp = timestamp });
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Polar reconciliation: JSON deserialization failed for webhook {WebhookId}.", webhookId);
            return Result<WebhookEvent, WebhookVerificationError>.Failure(
                new WebhookVerificationError("Event deserialization failed."));
        }
    }

    private static bool TryParseTimestamp(string value, out DateTimeOffset result)
    {
        result = default;
        if (!long.TryParse(value, out var epochSeconds)) return false;
        result = DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
        return true;
    }
}
