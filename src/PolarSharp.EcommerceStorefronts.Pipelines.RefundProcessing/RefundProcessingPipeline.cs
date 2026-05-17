using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.RefundProcessing;

/// <summary>
/// Orchestrator for the refund-processing pipeline. Resolves every registered
/// <see cref="IRefundProcessingStage"/> from DI, sorts them by
/// <see cref="IRefundProcessingStage.Order"/> ascending, and chains them so each
/// stage's output feeds the next.
/// </summary>
/// <remarks>
/// Mirrors the order-processing orchestrator in shape and exception-handling
/// conventions; see <c>OrderProcessingPipeline</c> for the rationale. The failure
/// reason key is <c>Pipelines.Refund.StageThrew</c>.
/// </remarks>
public sealed class RefundProcessingPipeline
{
    private readonly IReadOnlyList<IRefundProcessingStage> _stages;
    private readonly ILogger<RefundProcessingPipeline> _logger;

    /// <summary>Initialises the orchestrator.</summary>
    /// <param name="stages">Every <see cref="IRefundProcessingStage"/> registered in DI.</param>
    /// <param name="logger">Logger for orchestrator-level diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stages"/> or <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public RefundProcessingPipeline(
        IEnumerable<IRefundProcessingStage> stages,
        ILogger<RefundProcessingPipeline> logger)
    {
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(logger);

        _stages = stages.OrderBy(s => s.Order).ToArray();
        _logger = logger;
    }

    /// <summary>The sorted set of stages this orchestrator will dispatch to.</summary>
    public IReadOnlyList<IRefundProcessingStage> Stages => _stages;

    /// <summary>
    /// Runs the pipeline for a single seed refund, yielding one
    /// <see cref="RefundInProcess"/> per stage that touches it.
    /// </summary>
    /// <param name="seed">The starting <see cref="RefundInProcess"/>.</param>
    /// <param name="context">Per-invocation ambient context.</param>
    /// <param name="ct">Cancellation observed between stages.</param>
    /// <returns>The yielded sequence of in-process states.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="seed"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public async IAsyncEnumerable<RefundInProcess> RunAsync(
        RefundInProcess seed,
        PipelineStageContext context,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(context);

        RefundInProcess current = seed;
        foreach (var stage in _stages)
        {
            if (current.Outcome != PipelineOutcome.Continue)
            {
                yield break;
            }

            RefundInProcess? next = null;
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
                _logger.LogError(ex, "Refund pipeline stage {StageName} threw for refund {RefundId}", stage.Name, current.RefundId);
                failureKey = "Pipelines.Refund.StageThrew";
            }

            if (failureKey is not null)
            {
                current = current with
                {
                    Outcome = PipelineOutcome.Failed,
                    FailureReasonKey = StorefrontOption<string>.Some(failureKey),
                    Diagnostics = PipelineDiagnosticsHelper.Append(current.Diagnostics, new StageDiagnostic(stage.Name, failureKey, DateTimeOffset.UtcNow)),
                };
                yield return current;
                yield break;
            }

            if (next is null)
            {
                current = current with { Outcome = PipelineOutcome.Halt };
                yield return current;
                yield break;
            }

            current = next;
            yield return current;
        }
    }

    private static async IAsyncEnumerable<RefundInProcess> SingletonAsync(
        RefundInProcess seed,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        yield return seed;
    }
}
