using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Helpers for per-scope tenant context hydration in non-HTTP background contexts.
/// Pairs with <see cref="IPolarTenantScopeInitializer"/> — see that interface for the
/// canonical resolve-then-apply pattern and the AsyncLocal scoping rationale.
/// </summary>
public static class TenantScopeExtensions
{
    /// <summary>
    /// Sets the current tenant on the scope's <see cref="IMultiTenantContextSetter"/>.
    /// </summary>
    /// <remarks>
    /// MUST be called in the same async-state-machine frame where the tenant context needs
    /// to persist for subsequent DbContext resolutions. Awaited helper methods that perform
    /// the set internally will NOT propagate the AsyncLocal mutation back to their caller —
    /// the value gets scoped to the helper's async frame.
    /// </remarks>
    /// <param name="scope">The current DI service scope's <see cref="IServiceProvider"/>.</param>
    /// <param name="tenant">The tenant to set as the current scope's tenant context.</param>
    public static void SetCurrentTenant(this IServiceProvider scope, PolarTenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(tenant);

        var setter = scope.GetRequiredService<IMultiTenantContextSetter>();
        setter.MultiTenantContext = new MultiTenantContext<PolarTenantInfo>(tenant);
    }
}
