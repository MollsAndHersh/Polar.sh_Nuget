using Microsoft.Extensions.Logging;
using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks;

/// <summary>
/// Base class for Polar webhook event handlers.
/// </summary>
/// <typeparam name="TEvent">
/// The Polar webhook event type this handler processes. Must derive from
/// <see cref="WebhookEvent"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// Inherit from this class, inject your domain services, and implement
/// <see cref="HandleCoreAsync"/>. The base class seals all infrastructure concerns
/// (structured logging, error wrapping, lifecycle hooks) so that the handler body
/// contains only domain logic.
/// </para>
/// <para>
/// Register with:
/// <code>
/// builder.Services
///     .AddPolarInfrastructure(builder.Configuration)
///     .AddPolarWebhooks()
///     .AddWebhookHandler&lt;OrderCreatedEvent, OrderCreatedHandler&gt;();
/// </code>
/// </para>
/// </remarks>
/// <example>
/// Production-ready payment fulfillment handler:
/// <code>
/// public sealed class OrderCreatedHandler : PolarWebhookHandlerBase&lt;OrderCreatedEvent&gt;
/// {
///     private readonly IOrderService _orders;
///     private readonly IEmailSender _email;
///
///     public OrderCreatedHandler(
///         IOrderService orders,
///         IEmailSender email,
///         ILogger&lt;OrderCreatedHandler&gt; logger) : base(logger)
///     {
///         _orders = orders;
///         _email  = email;
///     }
///
///     protected override async Task HandleCoreAsync(OrderCreatedEvent @event, CancellationToken ct)
///     {
///         await _orders.FulfillAsync(@event.Data.Id, ct);
///         await _email.SendConfirmationAsync(@event.Data.Customer.Email, ct);
///     }
/// }
/// </code>
/// </example>
public abstract class PolarWebhookHandlerBase<TEvent> : IPolarWebhookHandler<TEvent>
    where TEvent : WebhookEvent
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="PolarWebhookHandlerBase{TEvent}"/>.
    /// </summary>
    /// <param name="logger">
    /// The logger used for structured event-received and event-handled log entries.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    protected PolarWebhookHandlerBase(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Handles the incoming Polar webhook event. Sealed — infrastructure concerns live here;
    /// implement <see cref="HandleCoreAsync"/> for domain logic.
    /// </summary>
    /// <param name="event">
    /// The strongly-typed, HMAC-verified, deserialized event payload.
    /// </param>
    /// <param name="ct">
    /// A bounded cancellation token. HTTP disconnection does not cancel this token —
    /// the library ensures in-flight handlers complete before the connection closes.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when the handler has finished.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="event"/> is <see langword="null"/>.
    /// </exception>
    public async Task HandleAsync(TEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        LogEventReceived(@event);
        try
        {
            await HandleCoreAsync(@event, ct).ConfigureAwait(false);
            LogEventHandled(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Polar webhook {EventType} {WebhookId}: handler threw an unhandled exception.",
                @event.Type, @event.WebhookId);
            await OnErrorAsync(@event, ex, ct).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Implement your handler logic here. Inject domain services via the constructor.
    /// </summary>
    /// <param name="event">
    /// The verified, deserialized Polar webhook event. Use <see cref="WebhookEvent.WebhookId"/>
    /// as an idempotency key — Polar delivers webhooks <em>at-least-once</em>, so the same
    /// <c>WebhookId</c> may arrive more than once.
    /// </param>
    /// <param name="ct">
    /// A bounded cancellation token. It is <em>not</em> the raw HTTP request token —
    /// HTTP client disconnection will not cancel this token mid-handler.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when processing is done.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Idempotency:</strong> Always check whether <see cref="WebhookEvent.WebhookId"/>
    /// has already been processed before performing side effects (DB upsert on the ID,
    /// distributed lock, or outbox pattern).
    /// </para>
    /// <para>
    /// <strong>Cancellation:</strong> If your handler needs a deadline, create your own
    /// <see cref="CancellationTokenSource"/> with an explicit timeout and link it to
    /// <paramref name="ct"/>.
    /// </para>
    /// </remarks>
    protected abstract Task HandleCoreAsync(TEvent @event, CancellationToken ct);

    /// <summary>
    /// Called when <see cref="HandleCoreAsync"/> throws an unhandled exception.
    /// Override to add custom error handling (alerting, dead-letter queue, etc.).
    /// </summary>
    /// <param name="event">The event that was being processed when the error occurred.</param>
    /// <param name="ex">The exception that was thrown.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when error handling is done.</returns>
    /// <remarks>
    /// The exception is re-thrown after <see cref="OnErrorAsync"/> returns, regardless
    /// of what this method does. Override to add side-effects only — do not suppress the
    /// exception here.
    /// </remarks>
    protected virtual Task OnErrorAsync(TEvent @event, Exception ex, CancellationToken ct)
        => Task.CompletedTask;

    private void LogEventReceived(TEvent @event) =>
        _logger.LogInformation(
            "Polar webhook received: {EventType} {WebhookId}",
            @event.Type, @event.WebhookId);

    private void LogEventHandled(TEvent @event) =>
        _logger.LogDebug(
            "Polar webhook handled successfully: {EventType} {WebhookId}",
            @event.Type, @event.WebhookId);
}
