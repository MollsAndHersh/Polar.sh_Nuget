using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.Logging;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Default <see cref="IPolarTenantScopeInitializer"/> implementation. Looks the tenant
/// up in the registered <see cref="IMultiTenantStore{TTenantInfo}"/> by primary key
/// and assigns it via Finbuckle's <see cref="IMultiTenantContextSetter"/> so any
/// <see cref="TenantAwareDbContextBase"/>-derived DbContext resolved later in the
/// scope sees the correct tenant.
/// </summary>
/// <remarks>
/// Finbuckle registers a single concrete accessor (typically
/// <c>AsyncLocalMultiTenantContextAccessor&lt;T&gt;</c>) that backs both
/// <see cref="IMultiTenantContextAccessor{T}"/> (read-side) and
/// <see cref="IMultiTenantContextSetter"/> (write-side). Setting via the setter is the
/// proper Finbuckle API for non-HTTP scope hydration — the concrete accessor's
/// <c>MultiTenantContext</c> property has a private setter and isn't reachable
/// through casts.
/// <para>
/// Registered automatically by <c>AddPolarReportingSnapshot</c> via <c>TryAddScoped</c>;
/// hosts that wire <see cref="IPolarTenantScopeInitializer"/> themselves before that
/// call retain their custom impl.
/// </para>
/// </remarks>
internal sealed class DefaultPolarTenantScopeInitializer(
    IMultiTenantStore<PolarTenantInfo> tenantStore,
    IMultiTenantContextSetter contextSetter,
    ILogger<DefaultPolarTenantScopeInitializer> logger)
    : IPolarTenantScopeInitializer
{
    /// <inheritdoc/>
    public async Task InitializeAsync(string tenantId, IServiceProvider scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrEmpty(tenantId);

        var tenant = await tenantStore.GetAsync(tenantId).ConfigureAwait(false);
        if (tenant is null)
        {
            logger.LogWarning(
                "PolarSharp tenant scope initializer: no tenant with Id '{TenantId}' found in the store. Scope will have no tenant context.",
                tenantId);
            return;
        }

        contextSetter.MultiTenantContext = new MultiTenantContext<PolarTenantInfo>(tenant);
    }
}
