using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing.Stages;

/// <summary>
/// Default stage that asks the configured shipping provider for available rates and
/// writes the chosen rate's amount to <see cref="OrderInProcess.ShippingCents"/>.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; concrete shipping-provider calls (Shippo,
/// EasyPost) are wired by Phase 27. Skipped automatically by the real implementation
/// when the order has no shipping address (digital-only carts).
/// </remarks>
public sealed class QuoteShippingStage : IOrderProcessingStage
{
    private readonly ILogger<QuoteShippingStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public QuoteShippingStage(ILogger<QuoteShippingStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.QuoteShipping;

    /// <inheritdoc/>
    public string Name => "QuoteShipping";

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
                Status = CheckoutStatus.ShippingQuoted,
                Diagnostics = PipelineDiagnosticsHelper.Append(
                    order.Diagnostics,
                    new StageDiagnostic(Name, "Pipelines.Stage.NotYetImplemented", DateTimeOffset.UtcNow)),
            };
        }
    }
}
