using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.SubscriptionBilling.Stages;

/// <summary>
/// Default stage that confirms the customer has a usable payment method on file
/// before the pipeline attempts to capture the cycle's charge.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; the real check (saved-card expiry, wallet
/// balance, mandate validity) lands in Phase 26.x.
/// </remarks>
public sealed class CheckPaymentMethodStage : ISubscriptionBillingStage
{
    private readonly ILogger<CheckPaymentMethodStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public CheckPaymentMethodStage(ILogger<CheckPaymentMethodStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.SubscriptionCheckPaymentMethod;

    /// <inheritdoc/>
    public string Name => "CheckPaymentMethod";

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
