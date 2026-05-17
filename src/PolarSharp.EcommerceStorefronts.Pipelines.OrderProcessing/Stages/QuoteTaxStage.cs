using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing.Stages;

/// <summary>
/// Default stage that asks the configured tax provider for a sales-tax quote and
/// writes the result to <see cref="OrderInProcess.TaxCents"/>.
/// </summary>
/// <remarks>
/// Phase 26 ships the skeleton only; concrete tax-provider calls
/// (<c>IStorefrontTaxProvider</c> in <c>PolarSharp.EcommerceStorefronts.Tax</c>)
/// are wired by Phase 27 once provider implementations land.
/// </remarks>
public sealed class QuoteTaxStage : IOrderProcessingStage
{
    private readonly ILogger<QuoteTaxStage> _logger;

    /// <summary>Initialises the stage.</summary>
    /// <param name="logger">Logger used to emit the not-yet-implemented breadcrumb.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public QuoteTaxStage(ILogger<QuoteTaxStage> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StageOrder.QuoteTax;

    /// <inheritdoc/>
    public string Name => "QuoteTax";

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
                Status = CheckoutStatus.TaxQuoted,
                Diagnostics = PipelineDiagnosticsHelper.Append(
                    order.Diagnostics,
                    new StageDiagnostic(Name, "Pipelines.Stage.NotYetImplemented", DateTimeOffset.UtcNow)),
            };
        }
    }
}
