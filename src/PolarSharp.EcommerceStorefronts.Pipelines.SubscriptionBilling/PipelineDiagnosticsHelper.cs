using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Pipelines.SubscriptionBilling;

/// <summary>
/// Internal utility for appending a <see cref="StageDiagnostic"/> to an existing
/// <see cref="IReadOnlyList{T}"/>. Centralised so stages do not duplicate the
/// allocation pattern.
/// </summary>
internal static class PipelineDiagnosticsHelper
{
    /// <summary>Returns a new list containing <paramref name="existing"/> followed by <paramref name="added"/>.</summary>
    /// <param name="existing">The pre-existing diagnostic trail.</param>
    /// <param name="added">The new diagnostic to append.</param>
    /// <returns>A read-only list containing both sequences.</returns>
    public static IReadOnlyList<StageDiagnostic> Append(IReadOnlyList<StageDiagnostic> existing, StageDiagnostic added)
    {
        var list = new List<StageDiagnostic>(existing.Count + 1);
        list.AddRange(existing);
        list.Add(added);
        return list;
    }
}
