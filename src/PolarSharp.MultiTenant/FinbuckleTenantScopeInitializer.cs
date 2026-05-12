using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PolarSharp.Webhooks;

namespace PolarSharp.MultiTenant;

/// <summary>
/// Populates the Finbuckle multi-tenant context for the current webhook HTTP request so
/// that handlers injecting <c>IMultiTenantContext&lt;PolarTenantInfo&gt;</c> receive the
/// correct tenant.
/// </summary>
/// <remarks>
/// <para>
/// Called by <c>HandleWebhookAsync</c> in <c>PolarSharp.Webhooks</c> after HMAC
/// verification succeeds and the tenant has been resolved via
/// <see cref="IWebhookTenantResolver"/>. The Finbuckle middleware does not run for the
/// webhook endpoint during regular strategy resolution — the tenant is determined instead
/// from the Polar <c>organization_id</c> field embedded in the payload.
/// </para>
/// <para>
/// Uses <c>HttpContext.SetTenantInfo&lt;PolarTenantInfo&gt;(tenantInfo, resetServiceProviderScope: false)</c>
/// — the same mechanism Finbuckle's own middleware uses — so that all downstream DI
/// consumers see a consistent tenant context.
/// </para>
/// </remarks>
internal sealed class FinbuckleTenantScopeInitializer(
    IMultiTenantStore<PolarTenantInfo> tenantStore,
    ILogger<FinbuckleTenantScopeInitializer> logger)
    : IWebhookTenantScopeInitializer
{
    /// <inheritdoc/>
    public async Task InitializeScopeAsync(HttpContext context, string tenantId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantId);

        // GetAsync(id) looks up by the tenant's Id (primary key), not Identifier.
        var tenantInfo = await tenantStore.GetAsync(tenantId).ConfigureAwait(false);
        if (tenantInfo is null)
        {
            logger.LogWarning(
                "PolarSharp MultiTenant: webhook arrived for tenant ID '{TenantId}' but no " +
                "matching tenant was found in the store. Handler will execute without tenant context.",
                tenantId);
            return;
        }

        // Use Finbuckle's HttpContext extension — the same API Finbuckle's own middleware uses.
        // resetServiceProviderScope: false — we're already inside the request scope; resetting
        // it would break the in-flight IServiceProvider chain for this request.
        context.SetTenantInfo<PolarTenantInfo>(tenantInfo, resetServiceProviderScope: false);

        logger.LogDebug(
            "PolarSharp MultiTenant: webhook tenant context set to '{TenantId}' ({TenantName}).",
            tenantId, tenantInfo.Name ?? tenantId);
    }
}
