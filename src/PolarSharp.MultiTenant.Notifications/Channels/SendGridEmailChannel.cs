using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.Notifications.Serialization;

namespace PolarSharp.MultiTenant.Notifications.Channels;

/// <summary>
/// <see cref="IEmailChannel"/> implementation backed by SendGrid's v3 Mail Send REST API.
/// </summary>
/// <remarks>
/// <para>
/// Posts to <c>https://api.sendgrid.com/v3/mail/send</c> using a hand-rolled HTTP request
/// — the official <c>SendGrid</c> NuGet package uses reflection-based serialization that
/// is not AOT-clean, so we issue the HTTP call directly and serialize the request body via
/// a source-generated <see cref="NotificationJsonContext"/>.
/// </para>
/// <para>
/// The API key is resolved at send time from the environment variable named by
/// <see cref="SendGridOptions.ApiKeyEnvVar"/>. This keeps the secret out of the appsettings
/// file and out of the configuration object's in-memory representation.
/// </para>
/// </remarks>
public sealed class SendGridEmailChannel : IEmailChannel
{
    /// <summary>The named <see cref="HttpClient"/> registered for this channel.</summary>
    public const string HttpClientName = "PolarSharp.MultiTenant.Notifications.SendGrid";

    private const string Endpoint = "https://api.sendgrid.com/v3/mail/send";
    private const int MaxResponseBodyChars = 4096;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<TenantNotificationOptions> _options;
    private readonly ILogger<SendGridEmailChannel> _logger;

    /// <summary>Initializes a new <see cref="SendGridEmailChannel"/>.</summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="options">Live options snapshot.</param>
    /// <param name="logger">Logger for delivery diagnostics.</param>
    public SendGridEmailChannel(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TenantNotificationOptions> options,
        ILogger<SendGridEmailChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SendAsync(RenderedNotification rendered, string toAddress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rendered);
        ArgumentException.ThrowIfNullOrWhiteSpace(toAddress);

        var opts = _options.CurrentValue;
        var apiKey = Environment.GetEnvironmentVariable(opts.Email.SendGrid.ApiKeyEnvVar);
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new TenantNotificationDeliveryException(
                "SendGrid",
                $"SendGrid API key environment variable '{opts.Email.SendGrid.ApiKeyEnvVar}' is not set; cannot dispatch.");
        }

        var payload = new SendGridMailRequest
        {
            Personalizations =
            [
                new SendGridPersonalization
                {
                    To = [new SendGridAddress { Email = toAddress }],
                    Subject = rendered.EmailSubject,
                },
            ],
            From = new SendGridAddress
            {
                Email = opts.Email.FromAddress,
                Name = opts.Email.FromDisplayName,
            },
            Content =
            [
                new SendGridContent
                {
                    Type = "text/plain",
                    Value = rendered.EmailBody,
                },
            ],
        };

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload, NotificationJsonContext.Default.SendGridMailRequest),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new TenantNotificationDeliveryException("SendGrid", "Transport failure during SendGrid mail send.", ex);
        }

        try
        {
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "SendGrid mail-send accepted for tenant {TenantId} ({Status} transition).",
                    rendered.Source.TenantId,
                    rendered.Source.NewStatus);
                return;
            }

            var body = await ReadResponseBodyAsync(response, ct).ConfigureAwait(false);
            _logger.LogError(
                "SendGrid mail-send returned HTTP {StatusCode} for tenant {TenantId}: {Body}",
                (int)response.StatusCode,
                rendered.Source.TenantId,
                body);
            throw new TenantNotificationDeliveryException("SendGrid", (int)response.StatusCode, body);
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

/// <summary>SendGrid v3 mail-send request shape (https://docs.sendgrid.com/api-reference/mail-send/mail-send).</summary>
internal sealed record SendGridMailRequest
{
    [JsonPropertyName("personalizations")]
    public required IReadOnlyList<SendGridPersonalization> Personalizations { get; init; }

    [JsonPropertyName("from")]
    public required SendGridAddress From { get; init; }

    [JsonPropertyName("content")]
    public required IReadOnlyList<SendGridContent> Content { get; init; }
}

internal sealed record SendGridPersonalization
{
    [JsonPropertyName("to")]
    public required IReadOnlyList<SendGridAddress> To { get; init; }

    [JsonPropertyName("subject")]
    public required string Subject { get; init; }
}

internal sealed record SendGridAddress
{
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed record SendGridContent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }
}
