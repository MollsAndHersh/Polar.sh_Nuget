using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace PolarSharp.MultiTenant.Identity.Authorization;

/// <summary>
/// Opt-in marker — combine with <see cref="RequirePolarPermissionAttribute"/> to allow an
/// AppMasterAdmin to operate outside their <see cref="ICurrentUser.CurrentTenantId"/>.
/// </summary>
/// <remarks>
/// <para>
/// Without this attribute, even an AppMasterAdmin's queries are bound to their current tenant
/// scope (via the EF Core global query filter and SQL RLS). With this attribute, AND after
/// the dual-flag verification (DB <see cref="PolarApplicationUser.IsAppMasterAdmin"/> +
/// <see cref="PolarRoles.AppMasterAdmin"/> role claim), the request-scoped
/// <see cref="IAppMasterAdminCrossTenantSignal"/> is set and downstream DbContexts honor it.
/// </para>
/// <para>
/// The attribute itself is metadata only — the activating logic lives in
/// <see cref="AllowCrossTenantEndpointFilter"/> registered automatically by
/// <c>AddPolarIdentity()</c>. Tenant admins (without AppMasterAdmin) hitting an
/// <c>[AllowCrossTenant]</c> route will fail the dual-flag check and the signal stays off —
/// the request continues but is bound to their normal tenant scope.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class AllowCrossTenantAttribute : AuthorizeAttribute, IAuthorizeData
{
    /// <summary>Initializes the attribute. The implicit policy is the AppMasterAdmin gate — without that, cross-tenant access is impossible regardless of opt-in.</summary>
    public AllowCrossTenantAttribute() => Policy = PolarAuthorizationPolicies.AppMasterAdmin;
}

/// <summary>
/// Endpoint filter that activates the cross-tenant signal when an
/// <see cref="AllowCrossTenantAttribute"/> is present on the matched endpoint AND the user
/// passes the AppMasterAdmin dual-flag check.
/// </summary>
internal sealed class AllowCrossTenantEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var hasAttribute = endpoint?.Metadata.GetMetadata<AllowCrossTenantAttribute>() is not null;
        if (hasAttribute)
        {
            var currentUser = context.HttpContext.RequestServices.GetService(typeof(ICurrentUser)) as ICurrentUser;
            if (currentUser?.IsAppMasterAdmin == true)
            {
                var signal = context.HttpContext.RequestServices.GetService(typeof(IAppMasterAdminCrossTenantSignal)) as IAppMasterAdminCrossTenantSignal;
                signal?.GrantCrossTenantAccess();
            }
        }
        return await next(context).ConfigureAwait(false);
    }
}
