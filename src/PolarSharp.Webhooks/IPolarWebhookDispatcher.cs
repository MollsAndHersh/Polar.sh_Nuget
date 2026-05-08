using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks;

/// <summary>
/// Routes a verified <see cref="WebhookEvent"/> to its registered
/// <see cref="IPolarWebhookHandler{TEvent}"/>.
/// </summary>
/// <remarks>
/// Internal contract — host applications interact with the webhook pipeline exclusively
/// through <see cref="IPolarWebhookHandler{TEvent}"/> implementations and the
/// <c>AddWebhookHandler&lt;TEvent, THandler&gt;()</c> registration method.
/// </remarks>
internal interface IPolarWebhookDispatcher
{
    /// <summary>
    /// Dispatches the given <paramref name="event"/> to its registered handler, if any.
    /// </summary>
    /// <param name="event">
    /// The HMAC-verified, deserialized Polar webhook event.
    /// </param>
    /// <param name="ct">
    /// A cancellation token representing the request lifetime (bounded by the library
    /// to prevent mid-handler cancellation from HTTP disconnects).
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when dispatch is done.</returns>
    Task DispatchAsync(WebhookEvent @event, CancellationToken ct);
}
