namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Shared abstraction for hydrating a Finbuckle multi-tenant context in execution contexts
/// that don't have an active HTTP request — primarily <c>IHostedService</c> background
/// workers (snapshot poller, webhook reconciler, fake-data sync service, etc.) and CLI
/// export jobs.
/// </summary>
/// <remarks>
/// <para>
/// Reuses the same Finbuckle resolution machinery that <c>PolarSharp.Webhooks</c>'s
/// <c>IWebhookTenantScopeInitializer</c> uses, without coupling the webhooks package to
/// <c>PolarSharp.MultiTenant.EntityFrameworkCore</c>. The concrete implementation in
/// <c>PolarSharp.MultiTenant</c>'s Finbuckle bridge implements BOTH interfaces — webhooks
/// and reporting share the machinery without webhooks gaining a new dependency.
/// </para>
/// <para>
/// <strong>Standalone webhooks preserved:</strong> the <c>IWebhookTenantScopeInitializer</c>
/// in <c>PolarSharp.Webhooks</c> is UNCHANGED. A host running webhooks without multi-tenant
/// installed gets the same null-impl fallback as v1.1.0.
/// </para>
/// </remarks>
public interface IPolarTenantScopeInitializer
{
    /// <summary>
    /// Hydrates the Finbuckle tenant context for the supplied tenant within the given service
    /// scope so that DbContexts and tenant-scoped services resolve correctly inside background
    /// workers.
    /// </summary>
    /// <param name="tenantId">The tenant's primary identifier (GUID string).</param>
    /// <param name="scope">The current DI service scope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when no tenant exists with the supplied ID.</exception>
    Task InitializeAsync(string tenantId, IServiceProvider scope, CancellationToken ct = default);
}
