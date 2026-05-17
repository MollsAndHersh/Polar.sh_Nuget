using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing;

/// <summary>
/// Orchestrator for the order-processing pipeline. Resolves every registered
/// <see cref="IOrderProcessingStage"/> from DI, sorts them by
/// <see cref="IOrderProcessingStage.Order"/> ascending, and chains them so each
/// stage's output feeds the next.
/// </summary>
/// <remarks>
/// The orchestrator is registered as a scoped service by
/// <c>OrderProcessingServiceCollectionExtensions.AddPolarOrderProcessingPipeline</c>.
/// Stages are sorted once in the constructor; per-invocation overhead is limited to
/// folding the single seed record through the chain.
/// <para>
/// Stage exceptions are caught at the orchestrator boundary and translated into a
/// terminal <see cref="OrderInProcess"/> with <see cref="PipelineOutcome.Failed"/>
/// and the localizable failure key <c>Pipelines.Order.StageThrew</c>. The exception
/// itself is logged at <see cref="LogLevel.Error"/> with the failing stage name.
/// </para>
/// </remarks>
public sealed class OrderProcessingPipeline
{
    private readonly IReadOnlyList<IOrderProcessingStage> _stages;
    private readonly ILogger<OrderProcessingPipeline> _logger;

    /// <summary>Initialises the orchestrator.</summary>
    /// <param name="stages">Every <see cref="IOrderProcessingStage"/> registered in DI.</param>
    /// <param name="logger">Logger for orchestrator-level diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stages"/> or <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public OrderProcessingPipeline(
        IEnumerable<IOrderProcessingStage> stages,
        ILogger<OrderProcessingPipeline> logger)
    {
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(logger);

        _stages = stages.OrderBy(s => s.Order).ToArray();
        _logger = logger;
    }

    /// <summary>The sorted set of stages this orchestrator will dispatch to.</summary>
    public IReadOnlyList<IOrderProcessingStage> Stages => _stages;

    /// <summary>
    /// Runs the pipeline for a single seed order, yielding one
    /// <see cref="OrderInProcess"/> per stage that touches it. The terminal element
    /// reflects the final cumulative state of the order.
    /// </summary>
    /// <param name="seed">The starting <see cref="OrderInProcess"/>.</param>
    /// <param name="context">Per-invocation ambient context.</param>
    /// <param name="ct">Cancellation observed between stages.</param>
    /// <returns>The yielded sequence of in-process states.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="seed"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public async IAsyncEnumerable<OrderInProcess> RunAsync(
        OrderInProcess seed,
        PipelineStageContext context,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(context);

        OrderInProcess current = seed;
        foreach (var stage in _stages)
        {
            if (current.Outcome != PipelineOutcome.Continue)
            {
                yield break;
            }

            OrderInProcess? next = null;
            string? failureKey = null;
            try
            {
                await foreach (var item in stage
                    .ProcessAsync(SingletonAsync(current, ct), context, ct)
                    .WithCancellation(ct)
                    .ConfigureAwait(false))
                {
                    next = item;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Pipeline orchestrator intentionally surfaces all stage failures as terminal records.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "Order pipeline stage {StageName} threw for order {OrderId}", stage.Name, current.OrderId);
                failureKey = "Pipelines.Order.StageThrew";
            }

            if (failureKey is not null)
            {
                current = current with
                {
                    Status = CheckoutStatus.Failed,
                    Outcome = PipelineOutcome.Failed,
                    FailureReasonKey = StorefrontOption<string>.Some(failureKey),
                    Diagnostics = PipelineDiagnosticsHelper.Append(current.Diagnostics, new StageDiagnostic(stage.Name, failureKey, DateTimeOffset.UtcNow)),
                };
                yield return current;
                yield break;
            }

            if (next is null)
            {
                // Stage filtered the record out; treat as halt rather than failure
                // so the orchestrator can terminate cleanly.
                current = current with { Outcome = PipelineOutcome.Halt };
                yield return current;
                yield break;
            }

            current = next;
            yield return current;
        }
    }

    private static async IAsyncEnumerable<OrderInProcess> SingletonAsync(
        OrderInProcess seed,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        yield return seed;
    }
}
