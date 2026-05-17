using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing.Stages;

/// <summary>
/// Default stage that re-validates every line item against the catalog (price snapshot,
/// availability, scope) before the order moves further down the pipeline.
/// </summary>
/// <remarks>
/// Phase 26 ships the architectural skeleton only; the real revalidation logic
/// (price recomputation, anti-tampering checks, catalog reads) lands in a Phase 26.x
/// patch. The skeleton transitions the order's status to
/// <see cref="CheckoutStatus.LineItemsValidated"/> and appends a diagnostic so the
/// downstream stages observe a coherent state.
/// </remarks>
public sealed class ValidateLineItemsStage : IOrderProcessingStage
{
    private readonly ILogger<ValidateLineItemsStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public ValidateLineItemsStage(ILogger<ValidateLineItemsStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.ValidateLineItems;

    /// <inheritdoc/>
    public string Name => "ValidateLineItems";

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
                Status = CheckoutStatus.LineItemsValidated,
                Diagnostics = PipelineDiagnosticsHelper.Append(
                    order.Diagnostics,
                    new StageDiagnostic(Name, "Pipelines.Stage.NotYetImplemented", DateTimeOffset.UtcNow)),
            };
        }
    }
}
