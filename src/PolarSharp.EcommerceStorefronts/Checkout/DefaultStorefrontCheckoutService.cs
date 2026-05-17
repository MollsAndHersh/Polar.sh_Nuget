using System.Runtime.CompilerServices;
using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;
using PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing;
using CartEntity = PolarSharp.EcommerceStorefronts.Abstractions.Cart.Cart;

namespace PolarSharp.EcommerceStorefronts.Checkout;

/// <summary>
/// Default implementation of <see cref="IStorefrontCheckoutService"/>. Registered by
/// <c>AddPolarStorefronts()</c>.
/// </summary>
/// <remarks>
/// <see cref="ProcessCheckoutAsync"/> behaviour depends on whether the order-
/// processing pipeline is registered:
/// <list type="bullet">
/// <item>
/// If <c>AddPolarOrderProcessingPipeline()</c> has been called, the service
/// constructs an <see cref="OrderInProcess"/> seed (against a stub cart for the
/// Phase 26 skeleton; Phase 25.x will swap in the real cart lookup) and folds the
/// pipeline output into <see cref="CheckoutStageStarted"/> +
/// <see cref="CheckoutStageCompleted"/> / <see cref="CheckoutFailed"/> /
/// <see cref="CheckoutSucceeded"/> events.
/// </item>
/// <item>
/// If the pipeline is not registered, the service yields a single
/// <see cref="CheckoutFailed"/> event with the reason key
/// <c>Checkout.PipelineNotRegistered</c> so hosts can detect the misconfiguration
/// from their UI without crashing.
/// </item>
/// </list>
/// </remarks>
public sealed class DefaultStorefrontCheckoutService : IStorefrontCheckoutService
{
    private const string NotImplementedMessage =
        "Storefront checkout initiation + session lookup are scheduled for Phase 25.x — see the storefront-core architecture section of the plan.";

    private readonly OrderProcessingPipeline? _pipeline;

    /// <summary>Initialises the service.</summary>
    /// <param name="pipeline">
    /// The order-processing pipeline, or <see langword="null"/> when
    /// <c>AddPolarOrderProcessingPipeline()</c> was not called. The constructor
    /// parameter is optional to keep the storefront-core service usable on hosts
    /// that have not yet opted into the pipeline.
    /// </param>
    public DefaultStorefrontCheckoutService(OrderProcessingPipeline? pipeline = null)
    {
        _pipeline = pipeline;
    }

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
    /// Yields a stream of pipeline events. When the order-processing pipeline is
    /// not registered, yields a single <see cref="CheckoutFailed"/> event so the
    /// host UI can render a coherent "checkout unavailable" surface.
    /// </remarks>
    public async IAsyncEnumerable<CheckoutPipelineEvent> ProcessCheckoutAsync(
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (_pipeline is null)
        {
            await Task.Yield();

            yield return new CheckoutFailed
            {
                SessionId = sessionId,
                At = DateTimeOffset.UtcNow,
                Stage = CheckoutStatus.Initiated,
                Error = new StorefrontProviderError(
                    Message: "The order-processing pipeline has not been registered. Call AddPolarOrderProcessingPipeline() during DI setup.",
                    CorrelationId: sessionId.ToString("N"),
                    Provider: "storefront:checkout-pipeline"),
            };
            yield break;
        }

        var startedAt = DateTimeOffset.UtcNow;
        yield return new CheckoutStageStarted
        {
            SessionId = sessionId,
            At = startedAt,
            Stage = CheckoutStatus.Initiated,
        };

        // Phase 26 skeleton: cart-lookup ships in Phase 25.x. For now, build a stub
        // cart so the pipeline plumbing is exercised end-to-end during development.
        var stubCart = new CartEntity
        {
            Id = sessionId,
            LineItems = [],
            Totals = new CartTotals
            {
                SubtotalCents = 0,
                GrandTotalCents = 0,
                Currency = "USD",
            },
            CreatedAt = startedAt,
        };

        var seed = new OrderInProcess
        {
            OrderId = sessionId,
            Cart = stubCart,
            CustomerId = StorefrontOption<Guid>.None,
            TenantId = StorefrontOption<Guid>.None,
            Currency = stubCart.Totals.Currency,
            Status = CheckoutStatus.Initiated,
        };

        var context = new PipelineStageContext
        {
            TenantId = StorefrontOption<Guid>.None,
            CustomerId = StorefrontOption<Guid>.None,
            CorrelationId = sessionId,
            StartedAt = startedAt,
        };

        await foreach (var state in _pipeline.RunAsync(seed, context, ct).WithCancellation(ct).ConfigureAwait(false))
        {
            if (state.Outcome == PipelineOutcome.Failed)
            {
                yield return new CheckoutFailed
                {
                    SessionId = sessionId,
                    At = DateTimeOffset.UtcNow,
                    Stage = state.Status,
                    Error = new StorefrontProviderError(
                        Message: state.FailureReasonKey.GetValueOrDefault("Checkout.PipelineStageFailed"),
                        CorrelationId: sessionId.ToString("N"),
                        Provider: "storefront:checkout-pipeline"),
                };
                yield break;
            }

            if (state.Status == CheckoutStatus.Completed)
            {
                yield return new CheckoutSucceeded
                {
                    SessionId = sessionId,
                    At = DateTimeOffset.UtcNow,
                    OrderId = state.OrderId.ToString("N"),
                };
                yield break;
            }

            yield return new CheckoutStageCompleted
            {
                SessionId = sessionId,
                At = DateTimeOffset.UtcNow,
                Stage = state.Status,
            };
        }
    }
}
