using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing.Stages;

/// <summary>
/// Default stage that applies cart-level and line-level discounts (coupon codes,
/// loyalty rewards, bundle savings) and writes the resulting amount to
/// <see cref="OrderInProcess.DiscountCents"/>.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; real discount math (coupon validation, stacking
/// rules, free-shipping promotions) lands in a Phase 26.x patch.
/// </remarks>
public sealed class ApplyDiscountsStage : IOrderProcessingStage
{
    private readonly ILogger<ApplyDiscountsStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public ApplyDiscountsStage(ILogger<ApplyDiscountsStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.ApplyDiscounts;

    /// <inheritdoc/>
    public string Name => "ApplyDiscounts";

    /// <inheritdoc/>
    public async IAsyncEnumerable<OrderInProcess> ProcessAsync(
        IAsyncEnumerable<OrderInProcess> input,
        PipelineStageContext context,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        await foreach (var order in input.WithCancellation(ct).ConfigureAwait(false))
        {
            _logger.LogDebug(
                "Phase 26.x: {StageName} not yet implemented; passing order {OrderId} through unchanged",
                Name,
                order.OrderId);

            yield return order with
            {
                Status = CheckoutStatus.DiscountsApplied,
                Diagnostics = PipelineDiagnosticsHelper.Append(
                    order.Diagnostics,
                    new StageDiagnostic(Name, "Pipelines.Stage.NotYetImplemented", DateTimeOffset.UtcNow)),
            };
        }
    }
}
