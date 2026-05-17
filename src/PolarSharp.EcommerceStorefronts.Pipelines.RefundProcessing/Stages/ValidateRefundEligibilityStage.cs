using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.RefundProcessing.Stages;

/// <summary>
/// Default stage that verifies the refund is allowed — the order exists, isn't
/// already fully refunded, falls inside the merchant's refund window, etc.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; the real eligibility checks (return-window
/// expiry, partial-refund accumulation, fraud holds) land in Phase 26.x.
/// </remarks>
public sealed class ValidateRefundEligibilityStage : IRefundProcessingStage
{
    private readonly ILogger<ValidateRefundEligibilityStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public ValidateRefundEligibilityStage(ILogger<ValidateRefundEligibilityStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.RefundValidateEligibility;

    /// <inheritdoc/>
    public string Name => "ValidateRefundEligibility";

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
