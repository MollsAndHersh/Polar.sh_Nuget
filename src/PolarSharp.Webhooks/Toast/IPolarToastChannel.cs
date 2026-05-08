using System.Threading.Channels;

namespace PolarSharp.Webhooks.Toast;

/// <summary>
/// Provides access to the Polar webhook real-time toast notification channel.
/// </summary>
/// <remarks>
/// <para>
/// Inject this interface into a Blazor layout component, SignalR hub, or SSE endpoint
/// and read from <see cref="Reader"/> to receive real-time UI notification payloads
/// as Polar events arrive.
/// </para>
/// <para>
/// Registered as a <c>Singleton</c> — one shared channel for all consumers. Multiple Blazor
/// circuits, SignalR connections, and background services all read from the same stream.
/// </para>
/// <para>
/// The channel is bounded (configured via <see cref="PolarToastOptions.ChannelCapacity"/>).
/// When full, new notifications are dropped with a <c>Debug</c> log entry — the underlying
/// business event has already been handled; only the real-time notification is missed.
/// </para>
/// <para>
/// Always call <see cref="PolarToastNotification.Localize"/> at render time, not at dispatch
/// time, to apply the correct culture per consuming user session.
/// </para>
/// <example>
/// Blazor Server consumption:
/// <code>
/// @inject IPolarToastChannel PolarToasts
/// @inject IPolarLocalizer Localizer
///
/// protected override Task OnInitializedAsync()
/// {
///     _ = Task.Run(ListenAsync);
///     return Task.CompletedTask;
/// }
///
/// private async Task ListenAsync()
/// {
///     await foreach (var toast in PolarToasts.Reader.ReadAllAsync(_cts.Token))
///     {
///         var localized = toast.Localize(Localizer);
///         await InvokeAsync(() => { Show(localized); StateHasChanged(); });
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public interface IPolarToastChannel
{
    /// <summary>
    /// Gets the channel reader for consuming <see cref="PolarToastNotification"/> values.
    /// </summary>
    /// <value>
    /// Consume with <c>await foreach (var toast in Reader.ReadAllAsync(ct)) { ... }</c>.
    /// </value>
    ChannelReader<PolarToastNotification> Reader { get; }

    /// <summary>
    /// Gets the channel writer used internally by the dispatcher to publish notifications.
    /// </summary>
    /// <value>
    /// Used only by <see cref="PolarWebhookDispatcher"/>. Host application code should
    /// use <see cref="Reader"/> for consumption only.
    /// </value>
    ChannelWriter<PolarToastNotification> Writer { get; }
}

/// <summary>
/// Default bounded channel implementation of <see cref="IPolarToastChannel"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="BoundedChannelOptions"/> with
/// <see cref="BoundedChannelFullMode.DropOldest"/> so that when the channel is full,
/// older (less relevant) notifications are dropped rather than blocking the webhook
/// response path or dropping the newest event.
/// </remarks>
internal sealed class PolarToastChannel : IPolarToastChannel
{
    private readonly Channel<PolarToastNotification> _channel;

    /// <summary>
    /// Initializes a new <see cref="PolarToastChannel"/> with the given capacity.
    /// </summary>
    /// <param name="capacity">
    /// The maximum number of notifications that can be queued before oldest entries
    /// are dropped. Sourced from <see cref="PolarToastOptions.ChannelCapacity"/>.
    /// </param>
    public PolarToastChannel(int capacity)
    {
        _channel = Channel.CreateBounded<PolarToastNotification>(new BoundedChannelOptions(capacity)
        {
            FullMode    = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false,
        });
    }

    /// <inheritdoc/>
    public ChannelReader<PolarToastNotification> Reader => _channel.Reader;

    /// <inheritdoc/>
    public ChannelWriter<PolarToastNotification> Writer => _channel.Writer;
}
