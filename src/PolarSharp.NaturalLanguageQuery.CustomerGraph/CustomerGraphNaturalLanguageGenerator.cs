namespace PolarSharp.NaturalLanguageQuery.CustomerGraph;

/// <summary>
/// Generates <c>CustomerGraphQuery</c> objects from natural-language input. The LLM
/// emits a JSON tree describing the desired query in terms of the typed builder DSL,
/// which gets deserialized into a <c>CustomerGraphQuery</c> instance via a sealed-record
/// schema — the LLM cannot inject raw openCypher / Gremlin / SQL into the query path.
/// </summary>
/// <remarks>
/// <para>
/// Per Case Study 04, audience-scoped builder filters apply: the Customer audience
/// cannot use <c>WhereSharesIpWith</c> or any IP-edge predicates; the Tenant audience
/// cannot reference cross-tenant fraud predicates; SaaSAdmin sees the full builder
/// surface. The LLM only sees the predicates it's allowed to compose — the audience
/// slice IS the LLM's allowed-builder-surface schema.
/// </para>
/// <para>
/// <strong>Phase 19 ships the package shell</strong>; the audience-aware schema slicer
/// + structured-output enforcement + dry-run validator land in Phase 19.x.
/// </para>
/// </remarks>
public interface ICustomerGraphNaturalLanguageGenerator
{
    /// <summary>Generates a CustomerGraphQuery from natural-language input.</summary>
    /// <param name="request">The NL query request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<NaturalLanguageQueryResponse> GenerateAsync(NaturalLanguageQueryRequest request, CancellationToken ct = default);
}
