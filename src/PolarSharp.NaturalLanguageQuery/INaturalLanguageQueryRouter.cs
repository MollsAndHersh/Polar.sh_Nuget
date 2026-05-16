namespace PolarSharp.NaturalLanguageQuery;

/// <summary>
/// Top-level entry point for natural-language → query translation. Hosts pass an NL
/// input + the resolved <see cref="AudienceScope"/> and get back a <see cref="NaturalLanguageQueryResponse"/>
/// containing the generated query + (optionally) the executed result.
/// </summary>
/// <remarks>
/// <para>
/// The router orchestrates the full pipeline per Case Study 04 "Audience-Scoped Schema Slicing":
/// </para>
/// <list type="number">
///   <item>Intent classifier picks the target (GraphQL vs CustomerGraphQuery).</item>
///   <item>Schema slice computed from the caller's <see cref="AudienceScope"/> + permissions.</item>
///   <item>LLM generates query against the sliced schema (structured-output enforcement).</item>
///   <item>Generated query dry-run validated for authorization.</item>
///   <item>Cost / complexity gate (max depth, max field count, RU/row estimate, timeout).</item>
///   <item>Optional execution (skipped on /preview endpoints).</item>
///   <item>Audit log entry with the full breakdown.</item>
/// </list>
/// </remarks>
public interface INaturalLanguageQueryRouter
{
    /// <summary>Translate natural-language input to a query AND execute it.</summary>
    /// <param name="request">The NL query request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<NaturalLanguageQueryResponse> TranslateAndExecuteAsync(NaturalLanguageQueryRequest request, CancellationToken ct = default);

    /// <summary>Translate natural-language input to a query WITHOUT executing it (preview).</summary>
    /// <param name="request">The NL query request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<NaturalLanguageQueryResponse> TranslateOnlyAsync(NaturalLanguageQueryRequest request, CancellationToken ct = default);
}

/// <summary>The user audiences served by the NL query feature.</summary>
public enum AudienceScope
{
    /// <summary>SaaS administrator with cross-tenant capability (requires explicit opt-in).</summary>
    SaaSAdmin,

    /// <summary>Tenant operator scoped to a single tenant's data.</summary>
    Tenant,

    /// <summary>End-customer scoped to their own data only.</summary>
    Customer,
}

/// <summary>Which query target the router should produce.</summary>
public enum PreferredQueryTarget
{
    /// <summary>Let the router's intent classifier decide.</summary>
    Auto,

    /// <summary>Force GraphQL (Reporting or Catalog schema).</summary>
    GraphQL,

    /// <summary>Force CustomerGraphQuery (typed graph DSL).</summary>
    CustomerGraph,
}

/// <summary>Request to translate natural-language input into an executable query.</summary>
/// <param name="NaturalLanguageInput">The user's plain-English question.</param>
/// <param name="AudienceScope">The caller's audience tier (computed by the host from the authenticated principal).</param>
/// <param name="TenantId">When the caller is operating within a tenant scope; null for SaaSAdmin cross-tenant ops + anonymous Customer requests.</param>
/// <param name="UserId">The caller's user id (when authenticated).</param>
/// <param name="PreferredTarget">Optional override of the router's intent classifier.</param>
/// <param name="CorrelationId">For request tracing across systems.</param>
public sealed record NaturalLanguageQueryRequest(
    string NaturalLanguageInput,
    AudienceScope AudienceScope,
    Guid? TenantId,
    Guid? UserId,
    PreferredQueryTarget PreferredTarget = PreferredQueryTarget.Auto,
    string? CorrelationId = null);

/// <summary>Response from a translate operation.</summary>
/// <param name="Target">Which query target was used.</param>
/// <param name="GeneratedQueryDocument">The generated query in its target-specific form (GraphQL document text OR CustomerGraphQuery JSON).</param>
/// <param name="ResultCount">When executed, the number of rows returned; null for preview-only.</param>
/// <param name="ResultsJson">When executed, the results as JSON; null for preview-only.</param>
/// <param name="AuditEntryId">The audit log entry recorded for this request.</param>
/// <param name="LlmProvider">Which AI provider generated the query.</param>
/// <param name="LlmModel">Which model.</param>
/// <param name="TotalDuration">End-to-end duration including LLM call + execution.</param>
/// <param name="RejectionReason">When non-null, the request was rejected; contains the localized reason key.</param>
public sealed record NaturalLanguageQueryResponse(
    string Target,
    string GeneratedQueryDocument,
    int? ResultCount,
    string? ResultsJson,
    Guid AuditEntryId,
    string LlmProvider,
    string LlmModel,
    TimeSpan TotalDuration,
    string? RejectionReason);
