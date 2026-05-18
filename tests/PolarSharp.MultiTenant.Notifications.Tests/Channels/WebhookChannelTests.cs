using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.Notifications.Channels;

namespace PolarSharp.MultiTenant.Notifications.Tests.Channels;

/// <summary>
/// Tests for <see cref="WebhookChannel"/> — JSON POST to a host-configured URL with an
/// HMAC-SHA256 signature header so the receiver can verify authenticity. Exercised via
/// <see cref="CapturingHttpMessageHandler"/> to assert URL, body, signature, and timeout
/// behaviour without firing a real HTTPS request.
/// </summary>
public sealed class WebhookChannelTests
{
    private const string SecretEnvVar = "POLARSHARP_TEST_WEBHOOK_SECRET";
    private const string SecretValue = "shhh-this-is-a-test-secret-value";
    private const string WebhookUrl = "https://webhooks.example.com/polar-events";

    // --- URL + body ------------------------------------------------------------------

    [Fact]
    public async Task PostAsync_posts_JSON_body_to_configured_url()
    {
        using var envScope = new EnvVarScope(SecretEnvVar, SecretValue);
        var handler = new CapturingHttpMessageHandler();
        var sut = NewSut(handler);

        await sut.PostAsync(Rendered(), CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(WebhookUrl, handler.LastRequest.RequestUri!.ToString());

        // Content-Type header set on the request body should be application/json.
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);

        // Verify the body is valid JSON with the expected shape (flattened payload).
        Assert.NotNull(handler.LastRequestBody);
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("tenantId", out _));
        Assert.True(root.TryGetProperty("tenantIdentifier", out _));
        Assert.True(root.TryGetProperty("previousStatus", out _));
        Assert.True(root.TryGetProperty("newStatus", out _));
        Assert.True(root.TryGetProperty("reason", out _));
        Assert.True(root.TryGetProperty("occurredAt", out _));
        Assert.True(root.TryGetProperty("renderedEmailSubject", out _));
        Assert.True(root.TryGetProperty("renderedEmailBody", out _));
        Assert.True(root.TryGetProperty("renderedSmsBody", out _));
    }

    // --- HMAC signature header --------------------------------------------------------

    [Fact]
    public async Task PostAsync_includes_HMAC_SHA256_signature_header()
    {
        using var envScope = new EnvVarScope(SecretEnvVar, SecretValue);
        var handler = new CapturingHttpMessageHandler();
        var sut = NewSut(handler);

        await sut.PostAsync(Rendered(), CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.True(
            handler.LastRequest!.Headers.TryGetValues(WebhookChannel.SignatureHeaderName, out var values),
            $"Expected header '{WebhookChannel.SignatureHeaderName}' on the request.");
        var signature = Assert.Single(values!);

        // Compute the expected signature ourselves over the captured body bytes and compare.
        Assert.NotNull(handler.LastRequestBody);
        var bodyBytes = Encoding.UTF8.GetBytes(handler.LastRequestBody!);
        var expectedHash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(SecretValue), bodyBytes);
        var expected = "sha256=" + Convert.ToHexStringLower(expectedHash);
        Assert.Equal(expected, signature);
    }

    [Fact]
    public async Task PostAsync_throws_DeliveryException_when_signing_secret_env_var_unset()
    {
        using var envScope = new EnvVarScope(SecretEnvVar, value: null);
        var handler = new CapturingHttpMessageHandler();
        var sut = NewSut(handler);

        var ex = await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.PostAsync(Rendered(), CancellationToken.None));

        Assert.Equal("Webhook", ex.ChannelName);
        Assert.Contains(SecretEnvVar, ex.Message);
        Assert.Equal(0, handler.CallCount);
    }

    // --- Timeout ----------------------------------------------------------------------

    [Fact]
    public async Task PostAsync_respects_configured_timeout_and_wraps_in_DeliveryException()
    {
        using var envScope = new EnvVarScope(SecretEnvVar, SecretValue);
        var handler = new CapturingHttpMessageHandler
        {
            // Delay longer than the HttpClient timeout we'll configure below.
            DelayBeforeResponse = TimeSpan.FromSeconds(5),
        };
        // Set the HttpClient timeout very short to force the channel's TaskCanceledException
        // branch (which represents a real HttpClient timeout — Twilio does not surface this
        // as HttpRequestException).
        var sut = NewSut(handler, httpClientTimeout: TimeSpan.FromMilliseconds(50));

        var ex = await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.PostAsync(Rendered(), CancellationToken.None));

        Assert.Equal("Webhook", ex.ChannelName);
        Assert.Contains("timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- Failure responses ------------------------------------------------------------

    [Fact]
    public async Task PostAsync_throws_NotificationDeliveryException_on_non_2xx_response()
    {
        using var envScope = new EnvVarScope(SecretEnvVar, SecretValue);
        var handler = new CapturingHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("webhook rejected payload"),
            },
        };
        var sut = NewSut(handler);

        var ex = await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.PostAsync(Rendered(), CancellationToken.None));

        Assert.Equal("Webhook", ex.ChannelName);
        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("webhook rejected payload", ex.ResponseBody!);
    }

    [Fact]
    public async Task PostAsync_throws_DeliveryException_on_HttpRequestException()
    {
        using var envScope = new EnvVarScope(SecretEnvVar, SecretValue);
        var handler = new CapturingHttpMessageHandler
        {
            ThrowOnSend = new HttpRequestException("connection refused"),
        };
        var sut = NewSut(handler);

        var ex = await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.PostAsync(Rendered(), CancellationToken.None));

        Assert.Equal("Webhook", ex.ChannelName);
        Assert.Contains("Transport failure", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    // --- helpers ----------------------------------------------------------------------

    private static WebhookChannel NewSut(
        CapturingHttpMessageHandler handler,
        TimeSpan? httpClientTimeout = null)
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Webhook.SigningSecretEnvVar = SecretEnvVar;
        opts.Webhook.Url = WebhookUrl;
        opts.Webhook.TimeoutSeconds = 10;

        return new WebhookChannel(
            new TestHttpClientFactory(handler, httpClientTimeout),
            new StaticOptionsMonitor<TenantNotificationOptions>(opts),
            NullLogger<WebhookChannel>.Instance);
    }

    private static RenderedNotification Rendered()
    {
        return new RenderedNotification
        {
            Source = TestHelpers.Notification(),
            EmailSubject = "Subject",
            EmailBody = "Body",
            SmsBody = "Sms",
        };
    }
}
