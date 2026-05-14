using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.Logging;
using PolarSharp.MultiTenant;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Default <see cref="IPolarTenantScopeInitializer"/> implementation. Looks the tenant up
/// in the registered <see cref="IMultiTenantStore{TTenantInfo}"/> by primary key and
/// returns it. Callers apply the result to their scope synchronously via
/// <see cref="TenantScopeExtensions.SetCurrentTenant"/>.
/// </summary>
/// <remarks>
/// Registered automatically by <c>AddPolarReportingSnapshot</c> via <c>TryAddScoped</c>;
/// hosts that wire <see cref="IPolarTenantScopeInitializer"/> themselves before that call
/// retain their custom impl.
/// </remarks>
internal sealed class DefaultPolarTenantScopeInitializer(
    IMultiTenantStore<PolarTenantInfo> tenantStore,
    ILogger<DefaultPolarTenantScopeInitializer> logger)
    : IPolarTenantScopeInitializer
{
    /// <inheritdoc/>
    public async Task<PolarTenantInfo?> ResolveTenantAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);

        var tenant = await tenantStore.GetAsync(tenantId).ConfigureAwait(false);
        if (tenant is null)
        {
            logger.LogWarning(
                "PolarSharp tenant scope initializer: no tenant with Id '{TenantId}' found in the store.",
                tenantId);
        }
        return tenant;
    }
}
