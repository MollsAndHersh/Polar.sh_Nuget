using PolarSharp.MultiTenant;

namespace PolarTestApp.Endpoints;

internal static class MultiTenantEndpoints
{
    internal static WebApplication MapMultiTenantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/multi-tenant").WithTags("MultiTenant");

        // GET /test/multi-tenant/orders
        // Requires X-Tenant-ID header (e.g. "alpha" or "beta" per appsettings.json).
        // Resolves the per-tenant PolarClient and lists that tenant's orders.
        group.MapGet("/orders", async (
            IMultiTenantPolarClientFactory factory,
            CancellationToken ct) =>
        {
            var polar = factory.GetClientForCurrentTenant();
            var result = await polar.Orders.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListOrdersMultiTenant")
        .WithSummary("List orders using the per-tenant Polar client resolved from the X-Tenant-ID header.")
        .WithDescription("""
            Supply an 'X-Tenant-ID' header matching one of the tenant identifiers configured
            in PolarSharp:MultiTenant:Tenants (e.g. 'alpha' or 'beta').

            Demonstrates per-tenant client resolution: each tenant gets its own PolarClient
            configured with that tenant's access token and server setting.
            """);

        group.MapGet("/subscriptions", async (
            IMultiTenantPolarClientFactory factory,
            CancellationToken ct) =>
        {
            var polar = factory.GetClientForCurrentTenant();
            var result = await polar.Subscriptions.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListSubscriptionsMultiTenant")
        .WithSummary("List subscriptions using the per-tenant Polar client resolved from the X-Tenant-ID header.");

        return app;
    }
}
