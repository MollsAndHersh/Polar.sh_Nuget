using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.Notifications.Channels;

namespace PolarSharp.MultiTenant.Notifications.Tests.Channels;

/// <summary>
/// Tests for <see cref="TwilioSmsChannel"/> — the hand-rolled Twilio Programmable Messages
/// HTTP client. Verified via <see cref="CapturingHttpMessageHandler"/> assertions on the
/// outgoing URL, Basic auth header, and form-encoded body without hitting the real Twilio
/// API.
/// </summary>
public sealed class TwilioSmsChannelTests
{
    private const string SidEnvVar = "POLARSHARP_TEST_TWILIO_SID";
    private const string TokenEnvVar = "POLARSHARP_TEST_TWILIO_TOKEN";
    private const string SidValue = "FAKE_SID_FOR_TESTS_ONLY";
    private const string TokenValue = "auth-token-fake-not-real";

    // --- Endpoint ---------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_posts_to_correct_Twilio_endpoint()
    {
        using var __ = new EnvVarScope(SidEnvVar, SidValue);
        using var ___ = new EnvVarScope(TokenEnvVar, TokenValue);
        var handler = new CapturingHttpMessageHandler();
        var sut = NewSut(handler);

        await sut.SendAsync(Rendered(), toNumber: "+15555550100", CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        var expectedUrl = $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(SidValue)}/Messages.json";
        Assert.Equal(expectedUrl, handler.LastRequest.RequestUri!.ToString());
    }

    // --- Basic auth header ------------------------------------------------------------

    [Fact]
    public async Task SendAsync_includes_Basic_auth_header()
    {
        using var __ = new EnvVarScope(SidEnvVar, SidValue);
        using var ___ = new EnvVarScope(TokenEnvVar, TokenValue);
        var handler = new CapturingHttpMessageHandler();
        var sut = NewSut(handler);

        await sut.SendAsync(Rendered(), toNumber: "+15555550100", CancellationToken.None);

        var auth = handler.LastRequest!.Headers.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Basic", auth!.Scheme);
        var expected = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{SidValue}:{TokenValue}"));
        Assert.Equal(expected, auth.Parameter);
    }

    // --- Form-encoded body ------------------------------------------------------------

    [Fact]
    public async Task SendAsync_includes_form_encoded_body()
    {
        using var __ = new EnvVarScope(SidEnvVar, SidValue);
        using var ___ = new EnvVarScope(TokenEnvVar, TokenValue);
        var handler = new CapturingHttpMessageHandler();
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Sms.Twilio.AccountSidEnvVar = SidEnvVar;
        opts.Sms.Twilio.AuthTokenEnvVar = TokenEnvVar;
        opts.Sms.Twilio.FromNumber = "+15558675309";
        var sut = NewSut(handler, opts);

        // Include URL-special characters in the body to confirm encoding.
        var rendered = Rendered(smsBody: "Hello & welcome, 50% off! See https://x.example/?q=a b");
        await sut.SendAsync(rendered, toNumber: "+15555550100", CancellationToken.None);

        Assert.NotNull(handler.LastRequestBody);
        var pairs = ParseForm(handler.LastRequestBody!);
        Assert.Equal("+15558675309", pairs["From"]);
        Assert.Equal("+15555550100", pairs["To"]);
        Assert.Equal("Hello & welcome, 50% off! See https://x.example/?q=a b", pairs["Body"]);

        // Content-Type should be application/x-www-form-urlencoded (set by FormUrlEncodedContent).
        Assert.Equal("application/x-www-form-urlencoded", handler.LastRequest!.Content!.Headers.ContentType!.MediaType);
    }

    // --- Failure responses -----------------------------------------------------------

    [Fact]
    public async Task SendAsync_throws_NotificationDeliveryException_on_non_2xx_response()
    {
        using var __ = new EnvVarScope(SidEnvVar, SidValue);
        using var ___ = new EnvVarScope(TokenEnvVar, TokenValue);
        var handler = new CapturingHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"code":21211,"message":"Invalid 'To' Phone Number"}"""),
            },
        };
        var sut = NewSut(handler);

        var ex = await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.SendAsync(Rendered(), toNumber: "+15555550100", CancellationToken.None));

        Assert.Equal("Twilio", ex.ChannelName);
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("Invalid 'To' Phone Number", ex.ResponseBody!);
    }

    [Fact]
    public async Task SendAsync_throws_DeliveryException_when_AccountSid_env_var_unset()
    {
        using var __ = new EnvVarScope(SidEnvVar, value: null);
        using var ___ = new EnvVarScope(TokenEnvVar, TokenValue);
        var handler = new CapturingHttpMessageHandler();
        var sut = NewSut(handler);

        var ex = await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.SendAsync(Rendered(), toNumber: "+15555550100", CancellationToken.None));

        Assert.Equal("Twilio", ex.ChannelName);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_throws_DeliveryException_when_AuthToken_env_var_unset()
    {
        using var __ = new EnvVarScope(SidEnvVar, SidValue);
        using var ___ = new EnvVarScope(TokenEnvVar, value: null);
        var handler = new CapturingHttpMessageHandler();
        var sut = NewSut(handler);

        var ex = await Assert.ThrowsAsync<TenantNotificationDeliveryException>(() =>
            sut.SendAsync(Rendered(), toNumber: "+15555550100", CancellationToken.None));

        Assert.Equal("Twilio", ex.ChannelName);
        Assert.Equal(0, handler.CallCount);
    }

    // --- helpers ---------------------------------------------------------------------

    private static TwilioSmsChannel NewSut(
        CapturingHttpMessageHandler handler,
        TenantNotificationOptions? opts = null)
    {
        var effective = opts ?? DefaultOptions();
        effective.Sms.Twilio.AccountSidEnvVar = SidEnvVar;
        effective.Sms.Twilio.AuthTokenEnvVar = TokenEnvVar;
        return new TwilioSmsChannel(
            new TestHttpClientFactory(handler),
            new StaticOptionsMonitor<TenantNotificationOptions>(effective),
            NullLogger<TwilioSmsChannel>.Instance);
    }

    private static TenantNotificationOptions DefaultOptions()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Sms.Twilio.FromNumber = "+15558675309";
        return opts;
    }

    private static RenderedNotification Rendered(string smsBody = "Default sms body")
    {
        return new RenderedNotification
        {
            Source = TestHelpers.Notification(),
            EmailSubject = "(email subject unused for sms tests)",
            EmailBody = "(email body unused for sms tests)",
            SmsBody = smsBody,
        };
    }

    private static Dictionary<string, string> ParseForm(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(pair[..idx].Replace('+', ' '));
            var value = Uri.UnescapeDataString(pair[(idx + 1)..].Replace('+', ' '));
            result[key] = value;
        }
        return result;
    }
}
