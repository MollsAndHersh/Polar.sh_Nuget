namespace PolarSharp.MultiTenant;

/// <summary>
/// Resolves a <see cref="PolarClient"/> scoped to the current request tenant.
/// </summary>
/// <remarks>
/// <para>
/// Inject this interface in Minimal API endpoints, MVC controllers, or services that
/// need to call the Polar API on behalf of a specific tenant.
/// </para>
/// <para>
/// Each tenant receives its own cached <see cref="PolarClient"/> instance (backed by a
/// dedicated <see cref="System.Net.Http.HttpClient"/> pre-configured with the tenant's
/// access token and server URL). Client instances are created lazily on first use and
/// cached for the application lifetime, so the factory is safe to call from any
/// number of concurrent threads.
/// </para>
/// <example>
/// <code>
/// app.MapGet("/orders", async (IMultiTenantPolarClientFactory factory, CancellationToken ct) =>
/// {
///     var polar = factory.GetClientForCurrentTenant();
///     return await polar.Orders.GetAsync(ct);
/// });
/// </code>
/// </example>
/// </remarks>
public interface IMultiTenantPolarClientFactory
{
    /// <summary>
    /// Returns the <see cref="PolarClient"/> associated with the current request's resolved tenant.
    /// </summary>
    /// <returns>
    /// A <see cref="PolarClient"/> pre-configured with the current tenant's access token
    /// and Polar server URL.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no tenant has been resolved for the current request (i.e., Finbuckle
    /// could not match the request to a registered tenant).
    /// </exception>
    PolarClient GetClientForCurrentTenant();
}
