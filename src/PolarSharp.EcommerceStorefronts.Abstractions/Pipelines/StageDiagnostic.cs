namespace PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

/// <summary>
/// Breadcrumb attached to a work-in-progress record (e.g. <see cref="OrderInProcess"/>)
/// every time a stage touches it. Stages append rather than replace so the final
/// record carries the complete provenance trail.
/// </summary>
/// <param name="StageName">The <c>Name</c> of the stage that produced the diagnostic.</param>
/// <param name="MessageKey">
/// A stable, localizable resource key (e.g. <c>"Pipelines.Stage.NotYetImplemented"</c>).
/// The actual user-facing string is resolved by the storefront UI, not the pipeline.
/// </param>
/// <param name="At">UTC timestamp the diagnostic was recorded.</param>
public sealed record StageDiagnostic(string StageName, string MessageKey, DateTimeOffset At);
