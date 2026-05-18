using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.Lifecycle;
using PolarSharp.MultiTenant.Notifications.Channels;

namespace PolarSharp.MultiTenant.Notifications.Tests;

/// <summary>
/// Shared test doubles + builders used across the Notifications test suite. Kept in one
/// file so the per-suite test classes stay focused on assertions.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Builds a <see cref="TenantStatusChangedNotification"/> with sensible defaults — every
    /// field can be overridden per call site.
    /// </summary>
    public static TenantStatusChangedNotification Notification(
        TenantStatus previous = TenantStatus.Active,
        TenantStatus next = TenantStatus.Suspended,
        string reason = "Test reason",
        string? tenantName = "ACME Corporation",
        string tenantIdentifier = "acme-corp",
        string? siteManagerEmail = "ops@acme.example",
        bool siteManagerEmailVerified = true,
        string? siteManagerPhone = "+15555550100",
        DateTimeOffset? occurredAt = null,
        Guid? tenantId = null,
        Guid? actorUserId = null)
    {
        return new TenantStatusChangedNotification
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            TenantIdentifier = tenantIdentifier,
            TenantName = tenantName,
            PreviousStatus = previous,
            NewStatus = next,
            Reason = reason,
            ActorUserId = actorUserId,
            OccurredAt = occurredAt ?? new DateTimeOffset(2026, 5, 18, 12, 30, 45, TimeSpan.Zero),
            SiteManagerEmail = siteManagerEmail ?? string.Empty,
            SiteManagerEmailVerified = siteManagerEmailVerified,
            SiteManagerPhone = siteManagerPhone,
        };
    }

    /// <summary>Builds a fully-valid <see cref="TenantNotificationOptions"/> with Enabled=true and all 3 channels on.</summary>
    public static TenantNotificationOptions FullyEnabledOptions() => new()
    {
        Enabled = true,
        EnabledChannels = new TenantNotificationChannels { Email = true, Sms = true, Webhook = true },
        Email = new EmailChannelOptions
        {
            FromAddress = "platform@example.com",
            FromDisplayName = "PolarSharp Platform",
            SendGrid = new SendGridOptions { ApiKeyEnvVar = "SENDGRID_API_KEY" },
        },
        Sms = new SmsChannelOptions
        {
            Twilio = new TwilioOptions
            {
                AccountSidEnvVar = "TWILIO_ACCOUNT_SID",
                AuthTokenEnvVar = "TWILIO_AUTH_TOKEN",
                FromNumber = "+15558675309",
            },
        },
        Webhook = new WebhookChannelOptions
        {
            Url = "https://webhooks.example.com/polar-events",
            SigningSecretEnvVar = "POLARSHARP_WEBHOOK_SECRET",
            TimeoutSeconds = 10,
        },
    };
}

/// <summary>
/// Minimal <see cref="IOptionsMonitor{TOptions}"/> stub returning a fixed instance. The SUTs
/// only ever read <see cref="CurrentValue"/>; OnChange callbacks are never invoked.
/// </summary>
internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    public StaticOptionsMonitor(T current) { CurrentValue = current; }
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>
/// <see cref="ILogger{T}"/> stub capturing every log call. Used to assert
/// failure-path logging behaviour without resorting to a snapshot framework.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = new();
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// Recording <see cref="IEmailChannel"/> stub. Captures every call's <see cref="RenderedNotification"/>
/// + recipient and can be configured to throw a <see cref="TenantNotificationDeliveryException"/>
/// from <see cref="SendAsync"/> to exercise the dispatcher's per-channel isolation.
/// </summary>
internal sealed class RecordingEmailChannel : IEmailChannel
{
    public List<(RenderedNotification Rendered, string ToAddress)> Calls { get; } = new();
    public Exception? ThrowOnSend { get; set; }
    public Func<Task>? BeforeReturn { get; set; }
    public int Order { get; private set; }
    public static int GlobalOrderCounter;

    public async Task SendAsync(RenderedNotification rendered, string toAddress, CancellationToken ct)
    {
        Order = System.Threading.Interlocked.Increment(ref GlobalOrderCounter);
        Calls.Add((rendered, toAddress));
        if (BeforeReturn is not null) await BeforeReturn().ConfigureAwait(false);
        if (ThrowOnSend is not null) throw ThrowOnSend;
    }
}

/// <summary>Recording <see cref="ISmsChannel"/> stub.</summary>
internal sealed class RecordingSmsChannel : ISmsChannel
{
    public List<(RenderedNotification Rendered, string ToNumber)> Calls { get; } = new();
    public Exception? ThrowOnSend { get; set; }
    public Func<Task>? BeforeReturn { get; set; }
    public int Order { get; private set; }

    public async Task SendAsync(RenderedNotification rendered, string toNumber, CancellationToken ct)
    {
        Order = System.Threading.Interlocked.Increment(ref RecordingEmailChannel.GlobalOrderCounter);
        Calls.Add((rendered, toNumber));
        if (BeforeReturn is not null) await BeforeReturn().ConfigureAwait(false);
        if (ThrowOnSend is not null) throw ThrowOnSend;
    }
}

/// <summary>Recording <see cref="IWebhookChannel"/> stub.</summary>
internal sealed class RecordingWebhookChannel : IWebhookChannel
{
    public List<RenderedNotification> Calls { get; } = new();
    public Exception? ThrowOnSend { get; set; }
    public Func<Task>? BeforeReturn { get; set; }
    public int Order { get; private set; }

    public async Task PostAsync(RenderedNotification rendered, CancellationToken ct)
    {
        Order = System.Threading.Interlocked.Increment(ref RecordingEmailChannel.GlobalOrderCounter);
        Calls.Add(rendered);
        if (BeforeReturn is not null) await BeforeReturn().ConfigureAwait(false);
        if (ThrowOnSend is not null) throw ThrowOnSend;
    }
}

/// <summary>
/// <see cref="DelegatingHandler"/>-derived stub that captures the most-recent outgoing
/// <see cref="HttpRequestMessage"/> + its body (read at SendAsync time) and returns a
/// configurable response. The captured body must be read here because the request content
/// is typically a one-shot stream that the production channel disposes after the send.
/// </summary>
internal sealed class CapturingHttpMessageHandler : DelegatingHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public HttpResponseMessage Response { get; set; } = new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(string.Empty),
    };
    public TimeSpan? DelayBeforeResponse { get; set; }
    public Exception? ThrowOnSend { get; set; }
    public int CallCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        if (DelayBeforeResponse is { } delay)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        if (ThrowOnSend is not null) throw ThrowOnSend;

        return Response;
    }
}

/// <summary>
/// Minimal <see cref="IHttpClientFactory"/> stub that always returns a single
/// <see cref="HttpClient"/> wired up to the supplied <see cref="CapturingHttpMessageHandler"/>.
/// </summary>
internal sealed class TestHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public TestHttpClientFactory(CapturingHttpMessageHandler handler, TimeSpan? timeout = null)
    {
        _client = new HttpClient(handler);
        if (timeout is { } t) _client.Timeout = t;
    }

    public HttpClient CreateClient(string name) => _client;
}

/// <summary>
/// Disposable scope for setting an environment variable during a test. Restores the previous
/// value on dispose so concurrent / subsequent tests are not affected.
/// </summary>
internal sealed class EnvVarScope : IDisposable
{
    private readonly string _name;
    private readonly string? _previous;

    public EnvVarScope(string name, string? value)
    {
        _name = name;
        _previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
}
