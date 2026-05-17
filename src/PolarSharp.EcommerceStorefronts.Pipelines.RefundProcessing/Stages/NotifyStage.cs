using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.RefundProcessing.Stages;

/// <summary>
/// Default terminal stage that dispatches refund-confirmation notifications — refund
/// receipt to the customer, ledger entry to the merchant.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; the dispatcher itself lives in the Phase 31
/// <c>PolarSharp.EcommerceStorefronts.Polar.Notifications</c> bridge.
/// </remarks>
public sealed class NotifyStage : IRefundProcessingStage
{
    private readonly ILogger<NotifyStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public NotifyStage(ILogger<NotifyStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.RefundNotify;

    /// <inheritdoc/>
    public string Name => "Notify";

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
