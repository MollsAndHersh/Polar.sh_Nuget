using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Toast;

namespace PolarSharp.Webhooks;

/// <summary>
/// Routes verified <see cref="WebhookEvent"/> objects to their registered
/// <see cref="IPolarWebhookHandler{TEvent}"/> implementations and optionally writes
/// <see cref="PolarToastNotification"/> payloads to <see cref="IPolarToastChannel"/>.
/// </summary>
/// <remarks>
/// <para>
/// AOT-safe dispatch: uses <see cref="IWebhookHandlerAdapter"/> instances registered at
/// startup time — no reflection at call time. The adapter is keyed by the concrete
/// <see cref="WebhookEvent"/> <see cref="Type"/> so lookup is an O(1) dictionary read.
/// </para>
/// <para>
/// <see cref="IPolarToastChannel"/> is optional — it is only injected when
/// <c>AddPolarToastNotifications()</c> has been called.
/// </para>
/// </remarks>
internal sealed class PolarWebhookDispatcher : IPolarWebhookDispatcher
{
    private readonly IReadOnlyDictionary<Type, IWebhookHandlerAdapter> _adapters;
    private readonly IOptionsMonitor<PolarWebhookOptions> _options;
    private readonly ILogger<PolarWebhookDispatcher> _logger;
    private readonly IPolarToastChannel? _toastChannel;

    /// <summary>
    /// Initializes the dispatcher with the set of adapters resolved from the DI scope.
    /// </summary>
    /// <param name="adapters">
    /// All registered <see cref="IWebhookHandlerAdapter"/> instances, one per event type.
    /// </param>
    /// <param name="options">Webhook configuration for warning thresholds and toast config.</param>
    /// <param name="logger">Logger for per-dispatch structured entries.</param>
    /// <param name="toastChannel">
    /// Optional toast channel. <see langword="null"/> when toast notifications are not registered.
    /// </param>
    public PolarWebhookDispatcher(
        IEnumerable<IWebhookHandlerAdapter> adapters,
        IOptionsMonitor<PolarWebhookOptions> options,
        ILogger<PolarWebhookDispatcher> logger,
        IPolarToastChannel? toastChannel = null)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _adapters     = adapters.ToDictionary(a => a.EventType);
        _options      = options;
        _logger       = logger;
        _toastChannel = toastChannel;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(WebhookEvent @event, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var eventType = @event.GetType();
        if (!_adapters.TryGetValue(eventType, out var adapter))
        {
            if (_options.CurrentValue.WarnOnUnhandledEventTypes)
                _logger.LogWarning(
                    "Polar webhook {EventType} {WebhookId}: no handler registered. " +
                    "The event will be acknowledged. Register a handler with " +
                    "AddWebhookHandler<{EventType}, THandler>().",
                    @event.Type, @event.WebhookId, @event.Type);
            return;
        }

        await adapter.HandleAsync(@event, ct).ConfigureAwait(false);

        TryWriteToast(@event, _options.CurrentValue);
    }

    private void TryWriteToast(WebhookEvent @event, PolarWebhookOptions opts)
    {
        if (_toastChannel is null) return;
        var toastOpts = opts.ToastNotifications;
        if (toastOpts is null || !toastOpts.Enabled) return;

        var config = toastOpts.Events.Find(c =>
            string.Equals(c.EventType, @event.Type, StringComparison.Ordinal));
        if (config is null) return;

        var tokens  = ToastPropertyExtractors.Extract(@event);
        var message = ToastTemplateRenderer.Render(config.MessageTemplate, tokens);
        var key     = @event.Type.Replace('.', '_').Replace('-', '_');

        var notification = new PolarToastNotification
        {
            EventType              = @event.Type,
            Title                  = config.Title,
            Message                = message,
            Severity               = config.Severity,
            Duration               = TimeSpan.FromSeconds(config.DurationSeconds),
            EventTimestamp         = @event.Timestamp,
            WebhookId              = @event.WebhookId,
            TitleLocalizationKey   = $"Toast_{key}_Title",
            MessageLocalizationKey = $"Toast_{key}_MessageTemplate",
            TokenValues            = tokens,
        };

        if (!_toastChannel.Writer.TryWrite(notification))
            _logger.LogDebug(
                "Polar toast channel is full ({Capacity} slots); notification for {EventType} dropped.",
                toastOpts.ChannelCapacity, @event.Type);
    }
}
