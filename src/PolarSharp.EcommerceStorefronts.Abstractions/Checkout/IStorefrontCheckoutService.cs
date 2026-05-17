namespace PolarSharp.EcommerceStorefronts.Abstractions.Checkout;

/// <summary>
/// Orchestrates the conversion of a cart into an order via the configured pipeline.
/// </summary>
/// <remarks>
/// <see cref="ProcessCheckoutAsync"/> returns the pipeline as an
/// <see cref="IAsyncEnumerable{T}"/> so the storefront UI can render progress as
/// each stage runs. The pipeline implementation lives in Phase 26 (the order-processing
/// pipeline package); this interface defines the storefront-facing surface.
/// </remarks>
public interface IStorefrontCheckoutService
{
    /// <summary>Creates a checkout session from the current cart.</summary>
    /// <param name="cmd">Customer-supplied checkout inputs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly-created <see cref="CheckoutSession"/> in
    /// <see cref="CheckoutStatus.Initiated"/> state.</returns>
    Task<StorefrontResult<CheckoutSession>> InitiateCheckoutAsync(
        InitiateCheckoutCommand cmd,
        CancellationToken ct);

    /// <summary>Loads an existing checkout session (for example after a payment redirect).</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The session, or <see cref="StorefrontNotFoundError"/> when unknown.</returns>
    Task<StorefrontResult<CheckoutSession>> GetSessionAsync(Guid sessionId, CancellationToken ct);

    /// <summary>
    /// Runs the order pipeline for the session, yielding one
    /// <see cref="CheckoutPipelineEvent"/> per state transition.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token observed between stages.</param>
    /// <returns>The pipeline event stream. Terminates with either
    /// <see cref="CheckoutSucceeded"/> or <see cref="CheckoutFailed"/>.</returns>
    IAsyncEnumerable<CheckoutPipelineEvent> ProcessCheckoutAsync(
        Guid sessionId,
        CancellationToken ct);
}
