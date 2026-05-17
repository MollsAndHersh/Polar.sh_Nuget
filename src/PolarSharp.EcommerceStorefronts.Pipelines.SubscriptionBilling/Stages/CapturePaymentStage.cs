using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.SubscriptionBilling.Stages;

/// <summary>
/// Default stage that captures funds for the subscription cycle's
/// <see cref="SubscriptionCycleInProcess.TotalCents"/>. Distinct class from the
/// order-pipeline's <c>CapturePaymentStage</c> so the two pipelines can route
/// captures through different payment providers when needed.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; the real capture call goes through the Phase 31
/// <c>PolarSharp.EcommerceStorefronts.Polar.Wallet</c> bridge.
/// </remarks>
public sealed class CapturePaymentStage : ISubscriptionBillingStage
{
    private readonly ILogger<CapturePaymentStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public CapturePaymentStage(ILogger<CapturePaymentStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.SubscriptionCapturePayment;

    /// <inheritdoc/>
    public string Name => "CapturePayment";

    /// <inheritdoc/>
    public async IAsyncEnumerable<SubscriptionCycleInProcess> ProcessAsync(
        IAsyncEnumerable<SubscriptionCycleInProcess> input,
        PipelineStageContext context,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        await foreach (var cycle in input.WithCancellation(ct).ConfigureAwait(false))
        {
            _logger.LogDebug(
                "Phase 26.x: {StageName} not yet implemented; passing subscription {SubscriptionId} through unchanged",
                Name,
                cycle.SubscriptionId);

            yield return cycle with
            {
                Diagnostics = PipelineDiagnosticsHelper.Append(
                    cycle.Diagnostics,
                    new StageDiagnostic(Name, "Pipelines.Stage.NotYetImplemented", DateTimeOffset.UtcNow)),
            };
        }
    }
}
