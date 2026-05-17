namespace PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

/// <summary>
/// A composable stage in the subscription-billing pipeline. Mirrors
/// <see cref="IOrderProcessingStage"/> in shape but operates on
/// <see cref="SubscriptionCycleInProcess"/> records.
/// </summary>
/// <remarks>
/// Discovery, ordering, cancellation, and exception conventions are identical to
/// <see cref="IOrderProcessingStage"/>; see its remarks for details. Each pipeline
/// has its own stage interface so the DI container can resolve the correct set
/// without runtime filtering.
/// </remarks>
public interface ISubscriptionBillingStage
{
    /// <summary>Relative position in the pipeline; lower runs first.</summary>
    int Order { get; }

    /// <summary>Human-readable name surfaced in logs and diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Processes a stream of in-flight subscription cycles, transforming each via
    /// <c>with</c>-expressions and yield-returning the result.
    /// </summary>
    /// <param name="input">The upstream stage's output stream.</param>
    /// <param name="context">Per-invocation ambient context.</param>
    /// <param name="ct">Cancellation observed between items.</param>
    /// <returns>The transformed stream.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is cancelled mid-stream.
    /// </exception>
    IAsyncEnumerable<SubscriptionCycleInProcess> ProcessAsync(
        IAsyncEnumerable<SubscriptionCycleInProcess> input,
        PipelineStageContext context,
        CancellationToken ct);
}
