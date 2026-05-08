using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks;

/// <summary>
/// Defines a handler for a specific Polar webhook event type.
/// </summary>
/// <typeparam name="TEvent">
/// The Polar webhook event type this handler processes. Must derive from
/// <see cref="WebhookEvent"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// Implement this interface (or inherit from <see cref="PolarWebhookHandlerBase{TEvent}"/>)
/// and register with the DI container via
/// <c>AddWebhookHandler&lt;TEvent, THandler&gt;()</c> on the fluent builder.
/// </para>
/// <para>
/// Handlers are registered as <c>Scoped</c> services and may inject <c>DbContext</c>,
/// <c>ICurrentUser</c>, and other scoped dependencies via the constructor.
/// </para>
/// <para>
/// Polar delivers webhooks <em>at-least-once</em>. The same event (identified by
/// <see cref="WebhookEvent.WebhookId"/>) may arrive more than once. Handler
/// implementations <strong>must</strong> be idempotent.
/// </para>
/// <example>
/// Simple handler that does not need infrastructure hooks:
/// <code>
/// public sealed class OrderCreatedHandler : IPolarWebhookHandler&lt;OrderCreatedEvent&gt;
/// {
///     private readonly IOrderService _orders;
///
///     public OrderCreatedHandler(IOrderService orders) => _orders = orders;
///
///     public Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
///         => _orders.FulfillAsync(@event.Data.Id, ct);
/// }
/// </code>
/// Prefer <see cref="PolarWebhookHandlerBase{TEvent}"/> to get automatic structured
/// logging around the handler call.
/// </example>
/// </remarks>
public interface IPolarWebhookHandler<in TEvent> where TEvent : WebhookEvent
{
    /// <summary>
    /// Processes the incoming Polar webhook event.
    /// </summary>
    /// <param name="event">
    /// The strongly-typed, HMAC-verified, deserialized event payload.
    /// </param>
    /// <param name="ct">
    /// A cancellation token. Note: this token is <em>not</em> the raw HTTP request token —
    /// the library wraps it in a non-cancellable scope so that HTTP client disconnection
    /// does not abort in-flight payment fulfillment.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when the handler has finished processing.</returns>
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
