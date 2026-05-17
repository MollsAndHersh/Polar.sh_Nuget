using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.SubscriptionBilling.Stages;

/// <summary>
/// Default terminal stage that dispatches subscription-cycle notifications — receipt
/// email, dunning notice on retry, merchant dashboard summary.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; the dispatcher itself lives in the Phase 31
/// <c>PolarSharp.EcommerceStorefronts.Polar.Notifications</c> bridge.
/// </remarks>
public sealed class NotifyStage : ISubscriptionBillingStage
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
    public int Order => StageOrder.SubscriptionNotify;

    /// <inheritdoc/>
    public string Name => "Notify";

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
