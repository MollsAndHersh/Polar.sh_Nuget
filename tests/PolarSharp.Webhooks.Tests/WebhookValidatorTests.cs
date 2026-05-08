using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace PolarSharp.Webhooks.Tests;

public sealed class WebhookValidatorTests
{
    // Raw bytes for a deterministic test secret.
    // Base64: "dGVzdFNlY3JldEtleUZvclBvbGFy" = "testSecretKeyForPolar"
    private const string TestSecretBase64 = "dGVzdFNlY3JldEtleUZvclBvbGFy";
    private const string WebhookId = "wh_test_01";

    private const string MinimalOrderJson =
        """{"type":"order.created","data":{"id":"ord_1"}}""";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WebhookValidator CreateValidator(PolarWebhookOptions opts)
    {
        var monitor = new StaticOptionsMonitor<PolarWebhookOptions>(opts);
        return new WebhookValidator(monitor, NullLogger<WebhookValidator>.Instance);
    }

    private static PolarWebhookOptions SingleSecretOptions(string secret, int toleranceSeconds = 300)
        => new() { Secret = secret, ToleranceSeconds = toleranceSeconds };

    private static string ComputeSignature(string secretBase64, string webhookId, string timestamp, byte[] body)
    {
        var secretBytes = Convert.FromBase64String(secretBase64);
        var prefix = Encoding.UTF8.GetBytes($"{webhookId}.{timestamp}.");
        var payload = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload, prefix.Length);
        var hmac = HMACSHA256.HashData(secretBytes, payload);
        return $"v1,{Convert.ToBase64String(hmac)}";
    }

    private static string CurrentUnixTimestamp()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    // ── Valid signature ────────────────────────────────────────────────────────

    [Fact]
    public void Verify_WithValidSignature_ReturnsSuccess()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);
        var ts = CurrentUnixTimestamp();
        var sig = ComputeSignature(TestSecretBase64, WebhookId, ts, body);

        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        var result = validator.Verify(WebhookId, ts, sig, body);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Verify_WithValidSignature_InjectsWebhookIdFromHeaders()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);
        var ts = CurrentUnixTimestamp();
        var sig = ComputeSignature(TestSecretBase64, WebhookId, ts, body);

        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        var result = validator.Verify(WebhookId, ts, sig, body);

        Assert.True(result.IsSuccess);
        var evt = result.Match(e => e, _ => throw new InvalidOperationException());
        Assert.Equal(WebhookId, evt.WebhookId);
    }

    [Fact]
    public void Verify_WithWhsecPrefixedSecret_ReturnsSuccess()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);
        var ts = CurrentUnixTimestamp();
        // Compute signature using raw base64 (the whsec_ prefix is stripped internally)
        var sig = ComputeSignature(TestSecretBase64, WebhookId, ts, body);

        var validator = CreateValidator(SingleSecretOptions($"whsec_{TestSecretBase64}"));
        var result = validator.Verify(WebhookId, ts, sig, body);

        Assert.True(result.IsSuccess);
    }

    // ── Invalid signature ──────────────────────────────────────────────────────

    [Fact]
    public void Verify_WithInvalidSignature_ReturnsFailure()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);
        var ts = CurrentUnixTimestamp();

        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        var result = validator.Verify(WebhookId, ts, "v1,aW52YWxpZHNpZ25hdHVyZQ==", body);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Verify_WithTamperedBody_ReturnsFailure()
    {
        var originalBody = Encoding.UTF8.GetBytes(MinimalOrderJson);
        var ts = CurrentUnixTimestamp();
        var sig = ComputeSignature(TestSecretBase64, WebhookId, ts, originalBody);

        var tamperedBody = Encoding.UTF8.GetBytes("""{"type":"order.created","data":{"id":"tampered"}}""");

        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        var result = validator.Verify(WebhookId, ts, sig, tamperedBody);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Verify_WithWrongSecret_ReturnsFailure()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);
        var ts = CurrentUnixTimestamp();
        // Sign with a different secret
        var sig = ComputeSignature("d3JvbmdTZWNyZXRLZXlGb3JQb2xhcg==", WebhookId, ts, body);

        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        var result = validator.Verify(WebhookId, ts, sig, body);

        Assert.True(result.IsFailure);
    }

    // ── Timestamp validation ───────────────────────────────────────────────────

    [Fact]
    public void Verify_WithExpiredTimestamp_ReturnsFailure()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);
        // Use a timestamp 10 minutes in the past (default tolerance is 5 minutes)
        var expiredTs = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var sig = ComputeSignature(TestSecretBase64, WebhookId, expiredTs, body);

        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        var result = validator.Verify(WebhookId, expiredTs, sig, body);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Verify_WithFutureTimestamp_ReturnsFailure()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);
        // Use a timestamp 10 minutes in the future
        var futureTs = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString();
        var sig = ComputeSignature(TestSecretBase64, WebhookId, futureTs, body);

        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        var result = validator.Verify(WebhookId, futureTs, sig, body);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Verify_WithNonNumericTimestamp_ReturnsFailure()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);

        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        var result = validator.Verify(WebhookId, "not-a-number", "v1,whatever", body);

        Assert.True(result.IsFailure);
    }

    // ── Multi-secret rotation ──────────────────────────────────────────────────

    [Fact]
    public void Verify_WithOldSecretDuringRotation_ReturnsSuccess()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);
        var ts = CurrentUnixTimestamp();
        const string oldSecretBase64 = "b2xkU2VjcmV0S2V5Rm9yUG9sYXI="; // "oldSecretKeyForPolar"
        // Signature computed with old secret
        var sig = ComputeSignature(oldSecretBase64, WebhookId, ts, body);

        var opts = new PolarWebhookOptions
        {
            // New secret listed first; old secret still active for rotation
            Secrets = [$"whsec_{TestSecretBase64}", oldSecretBase64],
        };
        var validator = CreateValidator(opts);
        var result = validator.Verify(WebhookId, ts, sig, body);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Verify_WithNewSecretDuringRotation_ReturnsSuccess()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);
        var ts = CurrentUnixTimestamp();
        // Signature computed with new secret
        var sig = ComputeSignature(TestSecretBase64, WebhookId, ts, body);

        const string oldSecretBase64 = "b2xkU2VjcmV0S2V5Rm9yUG9sYXI=";
        var opts = new PolarWebhookOptions
        {
            Secrets = [TestSecretBase64, oldSecretBase64],
        };
        var validator = CreateValidator(opts);
        var result = validator.Verify(WebhookId, ts, sig, body);

        Assert.True(result.IsSuccess);
    }

    // ── Multiple signatures in header ──────────────────────────────────────────

    [Fact]
    public void Verify_WithCommaDelimitedSignatures_AcceptsValidOne()
    {
        var body = Encoding.UTF8.GetBytes(MinimalOrderJson);
        var ts = CurrentUnixTimestamp();
        var validSig = ComputeSignature(TestSecretBase64, WebhookId, ts, body);
        // Header contains multiple signatures; one of them is valid
        var headerValue = $"v1,aW52YWxpZA==,{validSig}";

        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        var result = validator.Verify(WebhookId, ts, headerValue, body);

        Assert.True(result.IsSuccess);
    }

    // ── Null guard clauses ─────────────────────────────────────────────────────

    [Fact]
    public void Verify_NullWebhookId_ThrowsArgumentNullException()
    {
        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        Assert.Throws<ArgumentNullException>(() =>
            validator.Verify(null!, "123", "v1,sig", Array.Empty<byte>()));
    }

    [Fact]
    public void Verify_NullTimestamp_ThrowsArgumentNullException()
    {
        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        Assert.Throws<ArgumentNullException>(() =>
            validator.Verify(WebhookId, null!, "v1,sig", Array.Empty<byte>()));
    }

    [Fact]
    public void Verify_NullSignature_ThrowsArgumentNullException()
    {
        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        Assert.Throws<ArgumentNullException>(() =>
            validator.Verify(WebhookId, "123", null!, Array.Empty<byte>()));
    }

    // ── Unknown event type ─────────────────────────────────────────────────────

    [Fact]
    public void Verify_WithUnknownEventType_ReturnsUnknownWebhookEvent()
    {
        var body = Encoding.UTF8.GetBytes("""{"type":"future.event","data":{}}""");
        var ts = CurrentUnixTimestamp();
        var sig = ComputeSignature(TestSecretBase64, WebhookId, ts, body);

        var validator = CreateValidator(SingleSecretOptions(TestSecretBase64));
        var result = validator.Verify(WebhookId, ts, sig, body);

        Assert.True(result.IsSuccess);
        var evt = result.Match(e => e, _ => throw new InvalidOperationException());
        Assert.IsType<Serialization.UnknownWebhookEvent>(evt);
        Assert.Equal("future.event", evt.Type);
    }

    // ── Inner helper ──────────────────────────────────────────────────────────

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
