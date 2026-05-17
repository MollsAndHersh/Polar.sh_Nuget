namespace PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

/// <summary>
/// Per-item outcome flag carried alongside the work-in-progress record as stages run.
/// </summary>
/// <remarks>
/// Stages do not return a <see cref="PipelineOutcome"/> directly — the value is set on
/// the in-flight record (e.g. <see cref="OrderInProcess"/>) so downstream stages can
/// short-circuit when an earlier stage marked the item <see cref="Halt"/> or
/// <see cref="Failed"/>. The orchestrator inspects the outcome between stages and
/// stops dispatching when the work-in-progress is no longer <see cref="Continue"/>.
/// </remarks>
public enum PipelineOutcome
{
    /// <summary>The item should continue through the remaining stages.</summary>
    Continue = 0,

    /// <summary>
    /// The item finished early but did not fail. Subsequent stages are skipped; the
    /// orchestrator yields the record as-is and terminates the stream successfully.
    /// </summary>
    Halt = 1,

    /// <summary>The item failed validation or an upstream provider rejected it.</summary>
    Failed = 2,
}
