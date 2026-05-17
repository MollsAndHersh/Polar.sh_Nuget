using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.RefundProcessing.Stages;

/// <summary>
/// Default stage that returns funds to the customer's original payment method (or
/// credits their prepaid wallet) for the
/// <see cref="RefundInProcess.ApprovedAmountCents"/>.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; the real refund call goes through the Phase 31
/// <c>PolarSharp.EcommerceStorefronts.Polar.Wallet</c> bridge.
/// </remarks>
public sealed class ExecuteRefundStage : IRefundProcessingStage
{
    private readonly ILogger<ExecuteRefundStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public ExecuteRefundStage(ILogger<ExecuteRefundStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.RefundExecute;

    /// <inheritdoc/>
    public string Name => "ExecuteRefund";

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
