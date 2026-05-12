using Microsoft.AspNetCore.Http;

namespace PolarSharp.Webhooks;

/// <summary>
/// Populates the multi-tenant context on the current HTTP request scope after a
/// webhook has been successfully HMAC-verified and tenant-resolved.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by <c>PolarSharp.MultiTenant</c> when <c>AddPolarMultiTenant()</c> is
/// registered. The implementation sets the Finbuckle <c>IMultiTenantContextAccessor</c>
/// so that handlers injecting <c>IMultiTenantContext&lt;PolarTenantInfo&gt;</c> receive
/// the correct tenant for the current webhook delivery.
/// </para>
/// <para>
/// This interface lives in <c>PolarSharp.Webhooks</c> (no Finbuckle dependency) so that
/// the webhook package stays decoupled from the multi-tenant package. The implementation
/// in <c>PolarSharp.MultiTenant</c> bridges the two.
/// </para>
/// </remarks>
public interface IWebhookTenantScopeInitializer
{
    /// <summary>
    /// Populates the multi-tenant context for the current HTTP request with the tenant
    /// identified by <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="context">The current HTTP request context.</param>
    /// <param name="tenantId">
    /// The tenant identifier string from <see cref="WebhookTenantResolution.TenantId"/>
    /// — matches <c>ITenantInfo.Id</c> in the Finbuckle tenant store.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when the scope has been initialized.</returns>
    Task InitializeScopeAsync(HttpContext context, string tenantId);
}
