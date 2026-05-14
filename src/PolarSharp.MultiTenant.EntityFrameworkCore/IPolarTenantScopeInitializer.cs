using PolarSharp.MultiTenant;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Per-scope tenant context hydration for non-HTTP background contexts — the snapshot
/// orchestrator's per-tick scope, hosted-service workers, CLI export jobs.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Two-phase by design.</strong> The async tenant lookup happens in
/// <see cref="ResolveTenantAsync"/>; the synchronous apply happens via
/// <see cref="TenantScopeExtensions.SetCurrentTenant"/> on the scope's
/// <see cref="System.IServiceProvider"/>. This split is required because Finbuckle's
/// <c>IMultiTenantContextSetter</c> writes to an <see cref="System.Threading.AsyncLocal{T}"/>:
/// mutations made inside an awaited <c>async</c> method DO NOT flow back to the caller after
/// the await (the AsyncLocal value is scoped to the async-state-machine that performed the
/// write). The caller MUST apply the tenant in their own frame.
/// </para>
/// <para>
/// Canonical pattern:
/// <code>
/// using var scope = scopeFactory.CreateScope();
/// var initializer = scope.ServiceProvider.GetRequiredService&lt;IPolarTenantScopeInitializer&gt;();
/// var tenant = await initializer.ResolveTenantAsync(tenantId, ct);
/// if (tenant is null) return;                                // unknown tenant — abort tick
/// scope.ServiceProvider.SetCurrentTenant(tenant);            // SYNC, in caller frame
/// var dbContext = scope.ServiceProvider.GetRequiredService&lt;PolarReportingDbContext&gt;();
/// </code>
/// </para>
/// </remarks>
public interface IPolarTenantScopeInitializer
{
    /// <summary>
    /// Resolves the tenant from the registered <see cref="Finbuckle.MultiTenant.Abstractions.IMultiTenantStore{T}"/>
    /// by primary key. Returns <see langword="null"/> when no tenant matches the supplied id —
    /// callers should treat this as a soft failure (skip the tick) rather than throw.
    /// </summary>
    /// <param name="tenantId">The tenant's primary key (GUID string).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved <see cref="PolarTenantInfo"/>, or <see langword="null"/> when not found.</returns>
    Task<PolarTenantInfo?> ResolveTenantAsync(string tenantId, CancellationToken ct = default);
}
