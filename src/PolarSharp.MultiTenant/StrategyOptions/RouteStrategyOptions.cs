namespace PolarSharp.MultiTenant.StrategyOptions;

/// <summary>
/// Configuration for the route-parameter-based tenant resolution strategy.
/// </summary>
public class RouteStrategyOptions
{
    /// <summary>
    /// Gets or sets the route parameter name used to identify the tenant.
    /// </summary>
    /// <value>Default: <c>tenantId</c>.</value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Route:Parameter</c>.</remarks>
    public string Parameter { get; set; } = "tenantId";
}
