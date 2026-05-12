using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.Webhooks;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Extensions;

namespace PolarSharp.IntegrationTests.Standalone;

/// <summary>
/// End-to-end integration tests for the standalone <c>PolarSharp.Webhooks</c> package
/// running inside the real <see cref="PolarWebhooksTestApp"/> host via
/// <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// These tests exercise the full ASP.NET Core pipeline — middleware, routing, DI, HMAC
/// verification, and event dispatch — as it runs in production, using the same
/// <c>appsettings.json</c> the app ships with.
/// </summary>
public sealed class StandaloneWebhookPipelineTests
    : IClassFixture<WebApplicationFactory<global::Program>>, IDisposable
{
    // ── Shared secret that matches the factory's overridden config ────────────
    private const string SecretBase64 = "dGVzdFNlY3JldEtleUZvclBvbGFy";   // "testSecretKeyForPolar"
    private const string WebhookPath  = "/hooks/polar";

    private readonly WebApplicationFactory<global::Program> _factory;
    private readonly HttpClient _client;

    public StandaloneWebhookPipelineTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Override the placeholder secret from appsettings.json with a
                // known test secret so the HMAC helpers in this file match.
                services.PostConfigure<PolarWebhookOptions>(opts =>
                {
                    opts.Secret           = SecretBase64;
                    opts.RequireHttps     = false;
                    opts.EnableRateLimiting = false;
                });
            });
        });

        _client = _factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    // ── DI smoke tests ────────────────────────────────────────────────────────

    [Fact]
    public void TestApp_RegistersWebhookValidator()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<WebhookValidator>());
    }

    [Fact]
    public void TestApp_RegistersIPolarWebhookDispatcher()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IPolarWebhookDispatcher>());
    }

    [Fact]
    public void TestApp_RegistersAllHandlerAdapters()
    {
        using var scope = _factory.Services.CreateScope();
        var adapters = scope.ServiceProvider.GetServices<IWebhookHandlerAdapter>().ToList();
        var registeredTypes = adapters.Select(a => a.EventType).ToHashSet();
        var missing = KnownWebhookEventTypes.All
            .Where(t => !registeredTypes.Contains(t))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(missing);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_ValidSignedOrderCreated_Returns200()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_int_order_01",
            eventJson: """{"type":"order.created","data":{"id":"ord_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidSignedSubscriptionActive_Returns200()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_int_sub_01",
            eventJson: """{"type":"subscription.active","data":{"id":"sub_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidSignedCheckoutCreated_Returns200()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_int_co_01",
            eventJson: """{"type":"checkout.created","data":{"id":"co_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidSignedCustomerCreated_Returns200()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_int_cust_01",
            eventJson: """{"type":"customer.created","data":{"id":"cust_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidSignedRefundCreated_Returns200()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_int_ref_01",
            eventJson: """{"type":"refund.created","data":{"id":"ref_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidSignedBenefitGrantCreated_Returns200()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_int_bg_01",
            eventJson: """{"type":"benefit_grant.created","data":{"id":"bg_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_UnknownEventType_Returns200_AndDoesNotThrow()
    {
        // Polar may add new event types — library must accept and acknowledge
        // unknown types rather than failing the delivery.
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_int_unknown_01",
            eventJson: """{"type":"future.event.type","data":{"id":"evt_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── All 28 known event types ──────────────────────────────────────────────

    [Theory]
    [InlineData("order.created",             "wh_int_all_01")]
    [InlineData("order.updated",             "wh_int_all_02")]
    [InlineData("order.paid",                "wh_int_all_03")]
    [InlineData("order.refunded",            "wh_int_all_04")]
    [InlineData("subscription.created",      "wh_int_all_05")]
    [InlineData("subscription.active",       "wh_int_all_06")]
    [InlineData("subscription.updated",      "wh_int_all_07")]
    [InlineData("subscription.canceled",     "wh_int_all_08")]
    [InlineData("subscription.uncanceled",   "wh_int_all_09")]
    [InlineData("subscription.past_due",     "wh_int_all_10")]
    [InlineData("subscription.revoked",      "wh_int_all_11")]
    [InlineData("checkout.created",          "wh_int_all_12")]
    [InlineData("checkout.updated",          "wh_int_all_13")]
    [InlineData("checkout.expired",          "wh_int_all_14")]
    [InlineData("customer.created",          "wh_int_all_15")]
    [InlineData("customer.updated",          "wh_int_all_16")]
    [InlineData("customer.state_changed",    "wh_int_all_17")]
    [InlineData("customer.deleted",          "wh_int_all_18")]
    [InlineData("product.created",           "wh_int_all_19")]
    [InlineData("product.updated",           "wh_int_all_20")]
    [InlineData("benefit.created",           "wh_int_all_21")]
    [InlineData("benefit.updated",           "wh_int_all_22")]
    [InlineData("benefit_grant.created",     "wh_int_all_23")]
    [InlineData("benefit_grant.updated",     "wh_int_all_24")]
    [InlineData("benefit_grant.cycled",      "wh_int_all_25")]
    [InlineData("benefit_grant.revoked",     "wh_int_all_26")]
    [InlineData("refund.created",            "wh_int_all_27")]
    [InlineData("refund.updated",            "wh_int_all_28")]
    public async Task Post_EachKnownEventType_Returns200(string eventType, string webhookId)
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: webhookId,
            eventJson: "{\"type\":\"" + eventType + "\",\"data\":{\"id\":\"evt_" + webhookId + "\"}}");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Signature failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task Post_InvalidSignature_Returns400()
    {
        var bodyBytes = Encoding.UTF8.GetBytes("""{"type":"order.created","data":{"id":"ord_1"}}""");
        var ts        = CurrentUnixTimestamp();

        var response = await SendWebhookAsync(bodyBytes, new Dictionary<string, string>
        {
            ["webhook-id"]        = "wh_int_badsig",
            ["webhook-timestamp"] = ts,
            ["webhook-signature"] = "v1,aW52YWxpZHNpZ25hdHVyZQ=="
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_TamperedBody_Returns400()
    {
        var originalJson  = """{"type":"order.created","data":{"id":"ord_1"}}""";
        var ts            = CurrentUnixTimestamp();
        var sig           = ComputeSignature(SecretBase64, "wh_int_tamper", ts,
                                Encoding.UTF8.GetBytes(originalJson));

        var tamperedBytes = Encoding.UTF8.GetBytes("""{"type":"order.created","data":{"id":"TAMPERED"}}""");

        var response = await SendWebhookAsync(tamperedBytes, new Dictionary<string, string>
        {
            ["webhook-id"]        = "wh_int_tamper",
            ["webhook-timestamp"] = ts,
            ["webhook-signature"] = sig
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_ExpiredTimestamp_Returns400()
    {
        var expiredTs = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var body      = Encoding.UTF8.GetBytes("""{"type":"order.created","data":{"id":"ord_1"}}""");
        var sig       = ComputeSignature(SecretBase64, "wh_int_exp", expiredTs, body);

        var response = await SendWebhookAsync(body, new Dictionary<string, string>
        {
            ["webhook-id"]        = "wh_int_exp",
            ["webhook-timestamp"] = expiredTs,
            ["webhook-signature"] = sig
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_MissingSignatureHeader_Returns400()
    {
        var body = Encoding.UTF8.GetBytes("""{"type":"order.created","data":{"id":"ord_1"}}""");
        var ts   = CurrentUnixTimestamp();

        var response = await SendWebhookAsync(body, new Dictionary<string, string>
        {
            ["webhook-id"]        = "wh_int_nosig",
            ["webhook-timestamp"] = ts
            // webhook-signature intentionally omitted
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Content-Type and method enforcement ───────────────────────────────────

    [Fact]
    public async Task Post_WrongContentType_Returns415()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_int_ct",
            eventJson: """{"type":"order.created","data":{"id":"ord_1"}}""");

        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        foreach (var (k, v) in headers)
            request.Headers.TryAddWithoutValidation(k, v);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Get_ToWebhookPath_Returns405()
    {
        var response = await _client.GetAsync(WebhookPath);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Post_ToWrongPath_Returns404()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_int_route",
            eventJson: """{"type":"order.created","data":{"id":"ord_1"}}""");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/wrong/path")
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        foreach (var (k, v) in headers)
            request.Headers.TryAddWithoutValidation(k, v);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Diagnostic endpoint ───────────────────────────────────────────────────

    [Fact]
    public async Task Get_RootDiagnostic_Returns200_WithExpectedFields()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PolarWebhooksTestApp", body);
        Assert.Contains("/hooks/polar", body);
    }

    // ── Multi-secret rotation ─────────────────────────────────────────────────

    [Fact]
    public async Task Post_SignedWithOldSecret_DuringRotation_Returns200()
    {
        const string oldSecretBase64 = "b2xkU2VjcmV0S2V5Rm9yUG9sYXI="; // "oldSecretKeyForPolar"

        using var rotationFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<PolarWebhookOptions>(opts =>
                {
                    // Both secrets active during rotation window.
                    opts.Secrets          = [SecretBase64, oldSecretBase64];
                    opts.RequireHttps     = false;
                    opts.EnableRateLimiting = false;
                });
            });
        });

        using var rotationClient = rotationFactory.CreateClient();

        // Sign with the OLD secret — must still be accepted.
        var (body, headers) = BuildSignedRequest(
            secretBase64: oldSecretBase64,
            webhookId: "wh_int_rotation",
            eventJson: """{"type":"order.created","data":{"id":"ord_1"}}""");

        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        foreach (var (k, v) in headers)
            request.Headers.TryAddWithoutValidation(k, v);

        var response = await rotationClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendWebhookAsync(
        byte[] body,
        Dictionary<string, string> webhookHeaders)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        foreach (var (key, value) in webhookHeaders)
            request.Headers.TryAddWithoutValidation(key, value);

        return await _client.SendAsync(request);
    }

    private static (byte[] Body, Dictionary<string, string> Headers) BuildSignedRequest(
        string webhookId,
        string eventJson,
        string? secretBase64 = null)
    {
        var secret = secretBase64 ?? SecretBase64;
        var ts     = CurrentUnixTimestamp();
        var body   = Encoding.UTF8.GetBytes(eventJson);
        var sig    = ComputeSignature(secret, webhookId, ts, body);

        return (body, new Dictionary<string, string>
        {
            ["webhook-id"]        = webhookId,
            ["webhook-timestamp"] = ts,
            ["webhook-signature"] = sig
        });
    }

    private static string ComputeSignature(string secretBase64, string webhookId, string ts, byte[] body)
    {
        var secretBytes = Convert.FromBase64String(secretBase64);
        var prefix      = Encoding.UTF8.GetBytes($"{webhookId}.{ts}.");
        var payload     = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload, prefix.Length);
        var hmac = HMACSHA256.HashData(secretBytes, payload);
        return $"v1,{Convert.ToBase64String(hmac)}";
    }

    private static string CurrentUnixTimestamp()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
}
