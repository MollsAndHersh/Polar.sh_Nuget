using System.Runtime.CompilerServices;
using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;

namespace PolarSharp.EcommerceStorefronts.Checkout;

/// <summary>
/// Skeleton implementation of <see cref="IStorefrontCheckoutService"/>. Registered by
/// <c>AddPolarStorefronts()</c>; replaced wholesale when Phase 26 ships the order
/// pipeline.
/// </summary>
/// <remarks>
/// <see cref="ProcessCheckoutAsync"/> yields a single <see cref="CheckoutFailed"/>
/// event and completes — that gives hosts a stable contract for streaming UI work
/// while the real pipeline is still under construction. The other two methods throw
/// <see cref="NotImplementedException"/>.
/// </remarks>
public sealed class DefaultStorefrontCheckoutService : IStorefrontCheckoutService
{
    private const string NotImplementedMessage =
        "Storefront checkout is scheduled for Phase 25.x / Phase 26 — see the storefront-core architecture section of the plan.";

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<CheckoutSession>> InitiateCheckoutAsync(
        InitiateCheckoutCommand cmd,
        CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<CheckoutSession>> GetSessionAsync(Guid sessionId, CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <remarks>
    /// Yields a single <see cref="CheckoutFailed"/> event signalling that the
    /// pipeline is not yet implemented, then completes. Streaming surface remains
    /// stable so host UI code does not need to rewrite when Phase 26 ships.
    /// </remarks>
    public async IAsyncEnumerable<CheckoutPipelineEvent> ProcessCheckoutAsync(
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Yield asynchronously so the call site can observe cancellation between
        // events when the real pipeline starts emitting many of them.
        await Task.Yield();

        yield return new CheckoutFailed
        {
            SessionId = sessionId,
            At = DateTimeOffset.UtcNow,
            Stage = CheckoutStatus.Initiated,
            Error = new StorefrontProviderError(
                Message: NotImplementedMessage,
                CorrelationId: sessionId.ToString("N"),
                Provider: "storefront:checkout-pipeline"),
        };
    }
}
