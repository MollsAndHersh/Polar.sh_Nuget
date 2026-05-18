using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.Lifecycle;
using PolarSharp.MultiTenant.Notifications.Channels;

namespace PolarSharp.MultiTenant.Notifications.Tests.Channels;

/// <summary>
/// Tests for <see cref="SendGridEmailChannel"/> — the hand-rolled SendGrid v3 Mail Send
/// HTTP client. Exercised via a <see cref="CapturingHttpMessageHandler"/> that intercepts
/// the outgoing request so we can assert URL, headers, and JSON body without hitting the
/// real SendGrid API.
/// </summary>
public sealed class SendGridEmailChannelTests
{
    private const string ApiKeyEnvVar = "POLARSHARP_TEST_SENDGRID_API_KEY";
    private const string ApiKeyValue = "SG.test-fake-key-not-real";

    // --- Endpoint + headers ------------------------------------------------------------

    [Fact]
    public async Task SendAsync_posts_to_correct_SendGrid_endpoint()
    {
        using var envScope = new EnvVarScope(ApiKeyEnvVar, ApiKeyValue);
        var handler = new CapturingHttpMessageHandler();
        var sut = NewSut(handler);

        await sut.SendAsync(Rendered(), toAddress: "to@example.com", CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.sendgrid.com/v3/mail/send", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_includes_Authorization_Bearer_header_from_env_var()
    {
        using var envScope = new EnvVarScope(ApiKeyEnvVar, ApiKeyValue);
        var handler = new CapturingHttpMessageHandler();
        var sut = NewSut(handler);

        await sut.SendAsync(Rendered(), toAddress: "to@example.com", CancellationToken.None);

        Assert.NotNull(handler.LastRequest!.Headers.Authorization);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal(ApiKeyValue, handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task SendAsync_throws_DeliveryException_when_api_key_env_var_unset()
    {
        // Ensure env var is unset for this call.
        using var envScope = new EnvVarScope(ApiKeyEnvVar, value: null);
        var handler = new CapturingHttpMessageHandler();
        var sut = NewSut(handler);

        var ex = await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.SendAsync(Rendered(), toAddress: "to@example.com", CancellationToken.None));

        Assert.Equal("SendGrid", ex.ChannelName);
        Assert.Contains(ApiKeyEnvVar, ex.Message);
        Assert.Equal(0, handler.CallCount);
    }

    // --- JSON body serialization -------------------------------------------------------

    [Fact]
    public async Task SendAsync_serializes_payload_correctly_per_SendGrid_v3_schema()
    {
        using var envScope = new EnvVarScope(ApiKeyEnvVar, ApiKeyValue);
        var handler = new CapturingHttpMessageHandler();
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Email.SendGrid.ApiKeyEnvVar = ApiKeyEnvVar;
        opts.Email.FromAddress = "sender@example.com";
        opts.Email.FromDisplayName = "PolarSharp Sender";
        var sut = NewSut(handler, opts);

        var rendered = Rendered(subject: "Subject Line", body: "Body content");
        await sut.SendAsync(rendered, toAddress: "recipient@example.com", CancellationToken.None);

        Assert.NotNull(handler.LastRequestBody);
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = doc.RootElement;

        // personalizations[0].to[0].email + .subject
        var personalizations = root.GetProperty("personalizations");
        Assert.Equal(1, personalizations.GetArrayLength());
        var personalization = personalizations[0];
        var toArray = personalization.GetProperty("to");
        Assert.Equal(1, toArray.GetArrayLength());
        Assert.Equal("recipient@example.com", toArray[0].GetProperty("email").GetString());
        Assert.Equal("Subject Line", personalization.GetProperty("subject").GetString());

        // from
        var from = root.GetProperty("from");
        Assert.Equal("sender@example.com", from.GetProperty("email").GetString());
        Assert.Equal("PolarSharp Sender", from.GetProperty("name").GetString());

        // content[0]
        var contentArray = root.GetProperty("content");
        Assert.Equal(1, contentArray.GetArrayLength());
        Assert.Equal("text/plain", contentArray[0].GetProperty("type").GetString());
        Assert.Equal("Body content", contentArray[0].GetProperty("value").GetString());

        // Content-Type header on the request body should be application/json.
        Assert.Equal("application/json", handler.LastRequest!.Content!.Headers.ContentType!.MediaType);
    }

    // --- Failure responses -------------------------------------------------------------

    [Fact]
    public async Task SendAsync_throws_NotificationDeliveryException_on_non_2xx_response()
    {
        using var envScope = new EnvVarScope(ApiKeyEnvVar, ApiKeyValue);
        var handler = new CapturingHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"errors":[{"message":"server exploded"}]}"""),
            },
        };
        var sut = NewSut(handler);

        var ex = await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.SendAsync(Rendered(), toAddress: "to@example.com", CancellationToken.None));

        Assert.Equal("SendGrid", ex.ChannelName);
        Assert.Equal(500, ex.StatusCode);
        Assert.NotNull(ex.ResponseBody);
        Assert.Contains("server exploded", ex.ResponseBody!);
    }

    [Fact]
    public async Task SendAsync_logs_response_body_on_failure()
    {
        using var envScope = new EnvVarScope(ApiKeyEnvVar, ApiKeyValue);
        var handler = new CapturingHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("error-body-marker"),
            },
        };
        var log = new RecordingLogger<SendGridEmailChannel>();
        var sut = NewSut(handler, log: log);

        await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.SendAsync(Rendered(), toAddress: "to@example.com", CancellationToken.None));

        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Error &&
            e.Message.Contains("error-body-marker", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_throws_DeliveryException_on_HttpRequestException()
    {
        using var envScope = new EnvVarScope(ApiKeyEnvVar, ApiKeyValue);
        var handler = new CapturingHttpMessageHandler
        {
            ThrowOnSend = new HttpRequestException("connection refused"),
        };
        var sut = NewSut(handler);

        var ex = await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.SendAsync(Rendered(), toAddress: "to@example.com", CancellationToken.None));

        Assert.Equal("SendGrid", ex.ChannelName);
        Assert.Contains("Transport failure", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    // --- helpers -----------------------------------------------------------------------

    private static SendGridEmailChannel NewSut(
        CapturingHttpMessageHandler handler,
        TenantNotificationOptions? opts = null,
        RecordingLogger<SendGridEmailChannel>? log = null)
    {
        var effective = opts ?? DefaultOptions();
        effective.Email.SendGrid.ApiKeyEnvVar = ApiKeyEnvVar;
        return new SendGridEmailChannel(
            new TestHttpClientFactory(handler),
            new StaticOptionsMonitor<TenantNotificationOptions>(effective),
            (ILogger<SendGridEmailChannel>?)log ?? NullLogger<SendGridEmailChannel>.Instance);
    }

    private static TenantNotificationOptions DefaultOptions()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Email.FromAddress = "sender@example.com";
        opts.Email.FromDisplayName = "Test Sender";
        return opts;
    }

    private static RenderedNotification Rendered(string subject = "Default subject", string body = "Default body")
    {
        return new RenderedNotification
        {
            Source = TestHelpers.Notification(),
            EmailSubject = subject,
            EmailBody = body,
            SmsBody = "(sms body unused for email tests)",
        };
    }
}
