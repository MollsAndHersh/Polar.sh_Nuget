namespace PolarSharp.NaturalLanguageQuery.HotChocolate;

/// <summary>
/// Generates GraphQL query documents from natural-language input against the audience-scoped
/// schema slices of the PolarSharp.Reporting.GraphQL + PolarSharp.EcommerceStoreManagement.GraphQL
/// schemas.
/// </summary>
/// <remarks>
/// <para>
/// Per Case Study 04 "Audience-Scoped Schema Slicing":
/// </para>
/// <list type="bullet">
///   <item>Schema slice is computed from caller's <c>AudienceScope</c> + actual permissions.</item>
///   <item>LLM receives ONLY the slice; cannot reference fields outside it.</item>
///   <item>Structured-output enforcement (Anthropic tool-use OR OpenAI <c>response_format=json_schema</c>) constrains the LLM's output to a valid GraphQL document shape.</item>
///   <item>Generated query parsed; Hot Chocolate's request executor runs it in <c>validation-only</c> mode for dry-run authorization.</item>
///   <item>Cost / complexity gate (depth, field count, RU estimate) before execution.</item>
/// </list>
/// <para>
/// <strong>Phase 19 ships the package shell</strong>; the schema-slice computer, LLM client integration
/// (re-using the v1.2 translation providers as <c>IAiCompletionClient</c>), dry-run validator,
/// and audit log writer all land in Phase 19.x.
/// </para>
/// </remarks>
public interface IHotChocolateNaturalLanguageGenerator
{
    /// <summary>Generates a GraphQL query document from natural-language input.</summary>
    /// <param name="request">The NL query request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<NaturalLanguageQueryResponse> GenerateAsync(NaturalLanguageQueryRequest request, CancellationToken ct = default);
}
