using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks;

/// <summary>
/// Non-generic bridge that allows the dispatcher to invoke a typed
/// <see cref="IPolarWebhookHandler{TEvent}"/> without reflection at call time.
/// </summary>
/// <remarks>
/// AOT-safe: the generic cast is resolved at registration time, not at dispatch time.
/// No <c>GetMethod</c> or <c>Activator.CreateInstance</c> is used.
/// </remarks>
internal interface IWebhookHandlerAdapter
{
    /// <summary>Gets the <see cref="WebhookEvent"/> concrete type this adapter handles.</summary>
    Type EventType { get; }

    /// <summary>
    /// Invokes the underlying <see cref="IPolarWebhookHandler{TEvent}"/> with the given event.
    /// </summary>
    /// <param name="event">The verified event. Must be assignable to <see cref="EventType"/>.</param>
    /// <param name="ct">Cancellation token passed through to the handler.</param>
    Task HandleAsync(WebhookEvent @event, CancellationToken ct);
}

/// <summary>
/// Typed implementation of <see cref="IWebhookHandlerAdapter"/> that resolves the handler
/// from the DI scope and invokes it with a safe downcast.
/// </summary>
/// <typeparam name="TEvent">The concrete event record type.</typeparam>
internal sealed class WebhookHandlerAdapter<TEvent>(
    IPolarWebhookHandler<TEvent> handler) : IWebhookHandlerAdapter
    where TEvent : WebhookEvent
{
    /// <inheritdoc/>
    public Type EventType => typeof(TEvent);

    /// <inheritdoc/>
    public Task HandleAsync(WebhookEvent @event, CancellationToken ct)
        => handler.HandleAsync((TEvent)@event, ct);
}
