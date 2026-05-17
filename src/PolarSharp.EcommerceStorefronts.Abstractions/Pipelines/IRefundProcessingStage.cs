namespace PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

/// <summary>
/// A composable stage in the refund-processing pipeline. Mirrors
/// <see cref="IOrderProcessingStage"/> in shape but operates on
/// <see cref="RefundInProcess"/> records.
/// </summary>
/// <remarks>
/// Discovery, ordering, cancellation, and exception conventions are identical to
/// <see cref="IOrderProcessingStage"/>; see its remarks for details.
/// </remarks>
public interface IRefundProcessingStage
{
    /// <summary>Relative position in the pipeline; lower runs first.</summary>
    int Order { get; }

    /// <summary>Human-readable name surfaced in logs and diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Processes a stream of in-flight refunds, transforming each via
    /// <c>with</c>-expressions and yield-returning the result.
    /// </summary>
    /// <param name="input">The upstream stage's output stream.</param>
    /// <param name="context">Per-invocation ambient context.</param>
    /// <param name="ct">Cancellation observed between items.</param>
    /// <returns>The transformed stream.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is cancelled mid-stream.
    /// </exception>
    IAsyncEnumerable<RefundInProcess> ProcessAsync(
        IAsyncEnumerable<RefundInProcess> input,
        PipelineStageContext context,
        CancellationToken ct);
}
