using PolarSharp.MultiTenant.StrategyOptions;

namespace PolarSharp.MultiTenant;

/// <summary>
/// Configuration options for PolarSharp multi-tenancy.
/// </summary>
/// <remarks>
/// <para>
/// Bound from <c>PolarSharp:MultiTenant</c> in <c>appsettings.json</c>.
/// </para>
/// <para>
/// Only the strategy sub-option matching the active <see cref="Strategy"/> is used at startup;
/// the others are ignored.
/// </para>
/// <example>
/// <code>
/// "PolarSharp": {
///   "MultiTenant": {
///     "Strategy": "Header",
///     "Header": { "Name": "X-Tenant-ID" },
///     "Tenants": [
///       { "Id": "t1", "Identifier": "acme", "PolarAccessToken": "tok_live_xxx", "Server": "Production" }
///     ]
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public class PolarMultiTenantOptions
{
    /// <summary>
    /// Gets or sets the tenant identification strategy.
    /// </summary>
    /// <value>Default: <see cref="TenantStrategy.Header"/>.</value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Strategy</c>.</remarks>
    public TenantStrategy Strategy { get; set; } = TenantStrategy.Header;

    /// <summary>
    /// Gets or sets header-strategy configuration.
    /// </summary>
    /// <value>Applied when <see cref="Strategy"/> is <see cref="TenantStrategy.Header"/>.</value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Header</c>.</remarks>
    public HeaderStrategyOptions Header { get; set; } = new();

    /// <summary>
    /// Gets or sets route-parameter-strategy configuration.
    /// </summary>
    /// <value>Applied when <see cref="Strategy"/> is <see cref="TenantStrategy.Route"/>.</value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Route</c>.</remarks>
    public RouteStrategyOptions Route { get; set; } = new();

    /// <summary>
    /// Gets or sets hostname-strategy configuration.
    /// </summary>
    /// <value>Applied when <see cref="Strategy"/> is <see cref="TenantStrategy.Hostname"/>.</value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Hostname</c>.</remarks>
    public HostnameStrategyOptions Hostname { get; set; } = new();

    /// <summary>
    /// Gets or sets JWT-claim-strategy configuration.
    /// </summary>
    /// <value>Applied when <see cref="Strategy"/> is <see cref="TenantStrategy.Claim"/>.</value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Claim</c>.</remarks>
    public ClaimStrategyOptions Claim { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of registered tenants.
    /// </summary>
    /// <value>
    /// Zero or more tenant entries. Each entry must have a unique <see cref="PolarTenantInfo.Identifier"/>
    /// and a non-empty <see cref="PolarTenantInfo.PolarAccessToken"/>.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Tenants</c>.</remarks>
    public List<PolarTenantInfo> Tenants { get; set; } = [];
}
