using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.RefundProcessing.Stages;

/// <summary>
/// Default stage that turns the customer's <see cref="RefundInProcess.RequestedAmountCents"/>
/// into the merchant-approved <see cref="RefundInProcess.ApprovedAmountCents"/>,
/// taking restocking fees, partial-line returns, and tax-reversal rules into account.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; the real amount-computation logic
/// (item-level prorated discount + tax reversal) lands in Phase 26.x.
/// </remarks>
public sealed class ComputeRefundAmountStage : IRefundProcessingStage
{
    private readonly ILogger<ComputeRefundAmountStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public ComputeRefundAmountStage(ILogger<ComputeRefundAmountStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.RefundComputeAmount;

    /// <inheritdoc/>
    public string Name => "ComputeRefundAmount";

    /// <inheritdoc/>
    public async IAsyncEnumerable<RefundInProcess> ProcessAsync(
        IAsyncEnumerable<RefundInProcess> input,
        PipelineStageContext context,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        await foreach (var refund in input.WithCancellation(ct).ConfigureAwait(false))
        {
            _logger.LogDebug(
                "Phase 26.x: {StageName} not yet implemented; passing refund {RefundId} through unchanged",
                Name,
                refund.RefundId);

            yield return refund with
            {
                Diagnostics = PipelineDiagnosticsHelper.Append(
                    refund.Diagnostics,
                    new StageDiagnostic(Name, "Pipelines.Stage.NotYetImplemented", DateTimeOffset.UtcNow)),
            };
        }
    }
}
