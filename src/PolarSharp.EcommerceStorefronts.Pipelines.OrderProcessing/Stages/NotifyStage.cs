using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing.Stages;

/// <summary>
/// Default terminal stage that dispatches customer + merchant notifications (order
/// receipt email, fulfillment notice, merchant dashboard toast) and transitions the
/// order to <see cref="CheckoutStatus.Completed"/>.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; the notification dispatcher itself lives in the
/// Phase 31 <c>PolarSharp.EcommerceStorefronts.Polar.Notifications</c> bridge —
/// storefront-core never references the v1.3 notifications dispatcher directly.
/// </remarks>
public sealed class NotifyStage : IOrderProcessingStage
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
    public int Order => StageOrder.Notify;

    /// <inheritdoc/>
    public string Name => "Notify";

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
                Status = CheckoutStatus.Completed,
                Diagnostics = PipelineDiagnosticsHelper.Append(
                    order.Diagnostics,
                    new StageDiagnostic(Name, "Pipelines.Stage.NotYetImplemented", DateTimeOffset.UtcNow)),
            };
        }
    }
}
