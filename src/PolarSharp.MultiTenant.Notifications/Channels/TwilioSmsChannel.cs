using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.Notifications.Channels;

/// <summary>
/// <see cref="ISmsChannel"/> implementation backed by Twilio's Programmable Messages REST API.
/// </summary>
/// <remarks>
/// <para>
/// Posts to <c>https://api.twilio.com/2010-04-01/Accounts/{AccountSid}/Messages.json</c>
/// using a hand-rolled HTTP call. The official <c>Twilio</c> NuGet package pulls in a large
/// dependency graph and uses reflection-based JSON / XML parsing that is not AOT-clean, so
/// we issue the form-encoded POST directly.
/// </para>
/// <para>
/// Twilio uses HTTP Basic auth: the username is the Account SID, the password is the Auth
/// Token. Both are resolved at send time from the environment variables named by
/// <see cref="TwilioOptions.AccountSidEnvVar"/> and <see cref="TwilioOptions.AuthTokenEnvVar"/>.
/// </para>
/// </remarks>
public sealed class TwilioSmsChannel : ISmsChannel
{
    /// <summary>The named <see cref="HttpClient"/> registered for this channel.</summary>
    public const string HttpClientName = "PolarSharp.MultiTenant.Notifications.Twilio";

    private const string BaseUrl = "https://api.twilio.com/2010-04-01/Accounts/";
    private const int MaxResponseBodyChars = 4096;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<TenantNotificationOptions> _options;
    private readonly ILogger<TwilioSmsChannel> _logger;

    /// <summary>Initializes a new <see cref="TwilioSmsChannel"/>.</summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="options">Live options snapshot.</param>
    /// <param name="logger">Logger for delivery diagnostics.</param>
    public TwilioSmsChannel(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TenantNotificationOptions> options,
        ILogger<TwilioSmsChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SendAsync(RenderedNotification rendered, string toNumber, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rendered);
        ArgumentException.ThrowIfNullOrWhiteSpace(toNumber);

        var opts = _options.CurrentValue;
        var accountSid = Environment.GetEnvironmentVariable(opts.Sms.Twilio.AccountSidEnvVar);
        var authToken = Environment.GetEnvironmentVariable(opts.Sms.Twilio.AuthTokenEnvVar);

        if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken))
        {
            throw new TenantNotificationDeliveryException(
                "Twilio",
                $"Twilio credentials are not set; expected environment variables '{opts.Sms.Twilio.AccountSidEnvVar}' " +
                $"and '{opts.Sms.Twilio.AuthTokenEnvVar}'.");
        }

        var url = BaseUrl + Uri.EscapeDataString(accountSid) + "/Messages.json";
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("From", opts.Sms.Twilio.FromNumber),
            new KeyValuePair<string, string>("To", toNumber),
            new KeyValuePair<string, string>("Body", rendered.SmsBody),
        });

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = form,
        };
        var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new TenantNotificationDeliveryException("Twilio", "Transport failure during Twilio SMS send.", ex);
        }

        try
        {
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Twilio SMS accepted for tenant {TenantId} ({Status} transition).",
                    rendered.Source.TenantId,
                    rendered.Source.NewStatus);
                return;
            }

            var body = await ReadResponseBodyAsync(response, ct).ConfigureAwait(false);
            _logger.LogError(
                "Twilio SMS send returned HTTP {StatusCode} for tenant {TenantId}: {Body}",
                (int)response.StatusCode,
                rendered.Source.TenantId,
                body);
            throw new TenantNotificationDeliveryException("Twilio", (int)response.StatusCode, body);
        }
        finally
        {
            response.Dispose();
        }
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
