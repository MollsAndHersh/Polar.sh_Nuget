namespace PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

/// <summary>
/// A composable stage in the order-processing pipeline. Implementations transform
/// in-flight <see cref="OrderInProcess"/> records as they stream through.
/// </summary>
/// <remarks>
/// Stages are discovered from DI and sorted by <see cref="Order"/> ascending — lower
/// values run first. Default-stage <see cref="Order"/> values come from
/// <see cref="StageOrder"/>; custom stages can position themselves between defaults
/// using arithmetic (e.g. <c>StageOrder.ApplyDiscounts + 500</c>) to slot a stage
/// between <c>ApplyDiscountsStage</c> and <c>QuoteTaxStage</c>.
/// <para>
/// Stages MUST be cancellation-aware via the supplied <see cref="CancellationToken"/>
/// and MUST NOT swallow exceptions. The orchestrator catches stage failures and
/// marks the order <see cref="PipelineOutcome.Failed"/>; swallowing inside the
/// stage hides the failure from observability.
/// </para>
/// </remarks>
public interface IOrderProcessingStage
{
    /// <summary>
    /// Relative position in the pipeline; lower runs first. Use
    /// <see cref="StageOrder"/> constants as the base when picking a value.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Human-readable name surfaced in logs and the
    /// <see cref="OrderInProcess.Diagnostics"/> breadcrumb trail. Default-stage
    /// names match the class name without the trailing <c>Stage</c> suffix; custom
    /// stages should pick a stable identifier so log queries remain readable across
    /// versions of the host application.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Processes a stream of in-flight orders, transforming each via
    /// <c>with</c>-expressions and yield-returning the result. A stage typically
    /// emits one record per input but may emit zero (filter) or many (fan-out)
    /// where it makes sense for the pipeline.
    /// </summary>
    /// <param name="input">The upstream stage's output stream.</param>
    /// <param name="context">Per-invocation ambient context (tenant, customer, correlation).</param>
    /// <param name="ct">Cancellation observed between items.</param>
    /// <returns>The transformed stream.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is cancelled mid-stream. Stages should
    /// let this exception propagate.
    /// </exception>
    IAsyncEnumerable<OrderInProcess> ProcessAsync(
        IAsyncEnumerable<OrderInProcess> input,
        PipelineStageContext context,
        CancellationToken ct);
}
