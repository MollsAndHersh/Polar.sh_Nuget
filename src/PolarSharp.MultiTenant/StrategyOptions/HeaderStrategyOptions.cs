namespace PolarSharp.MultiTenant.StrategyOptions;

/// <summary>
/// Configuration for the header-based tenant resolution strategy.
/// </summary>
public class HeaderStrategyOptions
{
    /// <summary>
    /// Gets or sets the HTTP request header name used to identify the tenant.
    /// </summary>
    /// <value>Default: <c>X-Tenant-ID</c>.</value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Header:Name</c>.</remarks>
    public string Name { get; set; } = "X-Tenant-ID";
}
