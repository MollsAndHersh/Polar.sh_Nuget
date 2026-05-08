namespace PolarSharp.MultiTenant.StrategyOptions;

/// <summary>
/// Configuration for the hostname-based tenant resolution strategy.
/// </summary>
public class HostnameStrategyOptions
{
    /// <summary>
    /// Gets or sets the hostname template used to extract the tenant identifier.
    /// </summary>
    /// <value>
    /// A glob-style template where <c>__tenant__</c> is the placeholder for the tenant identifier.
    /// Default: <c>__tenant__.*</c> (matches the first subdomain as the tenant).
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Hostname:Template</c>.</remarks>
    public string Template { get; set; } = "__tenant__.*";
}
