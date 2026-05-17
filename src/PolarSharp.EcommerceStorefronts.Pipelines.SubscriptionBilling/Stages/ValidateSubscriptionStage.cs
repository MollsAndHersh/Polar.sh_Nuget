using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.SubscriptionBilling.Stages;

/// <summary>
/// Default stage that validates the subscription is in a billable state (active, not
/// paused, period boundaries make sense) before further stages run.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; the real validation logic (state-machine checks,
/// dunning-window enforcement, plan-change reconciliation) lands in Phase 26.x.
/// </remarks>
public sealed class ValidateSubscriptionStage : ISubscriptionBillingStage
{
    private readonly ILogger<ValidateSubscriptionStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public ValidateSubscriptionStage(ILogger<ValidateSubscriptionStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.SubscriptionValidate;

    /// <inheritdoc/>
    public string Name => "ValidateSubscription";

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
