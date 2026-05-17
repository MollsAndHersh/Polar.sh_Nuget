using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.SubscriptionBilling.Stages;

/// <summary>
/// Default stage that computes proration adjustments — mid-cycle plan upgrades,
/// downgrades, refunds for unused time — and writes the signed amount to
/// <see cref="SubscriptionCycleInProcess.ProrationCents"/>.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; the real proration math (day-count vs.
/// usage-based, credit-balance application) lands in Phase 26.x.
/// </remarks>
public sealed class ApplyProrationStage : ISubscriptionBillingStage
{
    private readonly ILogger<ApplyProrationStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public ApplyProrationStage(ILogger<ApplyProrationStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.SubscriptionApplyProration;

    /// <inheritdoc/>
    public string Name => "ApplyProration";

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
