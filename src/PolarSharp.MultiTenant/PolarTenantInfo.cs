using Finbuckle.MultiTenant.Abstractions;

namespace PolarSharp.MultiTenant;

/// <summary>
/// Extends Finbuckle's <see cref="IMultiTenantContext"/> with Polar.sh-specific
/// per-tenant configuration.
/// </summary>
/// <remarks>
/// <para>
/// One instance per registered tenant. Finbuckle resolves the correct instance from the
/// configured tenant store on each request. PolarSharp reads <see cref="PolarAccessToken"/>
/// and <see cref="Server"/> to construct and cache the per-tenant <see cref="PolarClient"/>.
/// </para>
/// <para>
/// Configure tenants in <c>appsettings.json</c> under <c>PolarSharp:MultiTenant:Tenants</c>
/// or register them programmatically via <c>AddPolarMultiTenant(opts => { ... })</c>.
/// </para>
/// </remarks>
public sealed class PolarTenantInfo : ITenantInfo
{
    /// <summary>Gets or sets the Finbuckle internal tenant ID.</summary>
    /// <value>A unique opaque identifier for this tenant (e.g., a GUID or short string).</value>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant identifier matched against the incoming request.</summary>
    /// <value>
    /// The value extracted from the header, route, hostname, or claim and matched against
    /// this tenant entry. Must be unique across all registered tenants.
    /// </value>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable tenant display name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the Polar Organization Access Token for this tenant.</summary>
    /// <value>
    /// The raw access token string (e.g., <c>tok_live_xxx</c> or <c>tok_sandbox_xxx</c>).
    /// Sent as <c>Authorization: Bearer &lt;token&gt;</c> on all Polar API calls for this tenant.
    /// </value>
    /// <remarks>
    /// Bound from <c>PolarSharp:MultiTenant:Tenants[n]:PolarAccessToken</c>.
    /// Never log or expose this value — it grants full organizational access to Polar.
    /// </remarks>
    public string PolarAccessToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the Polar server environment for this tenant.</summary>
    /// <value>
    /// <see cref="PolarServer.Production"/> (default) targets <c>https://api.polar.sh/v1</c>.
    /// <see cref="PolarServer.Sandbox"/> targets <c>https://sandbox-api.polar.sh/v1</c>.
    /// </value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Tenants[n]:Server</c>.</remarks>
    public PolarServer Server { get; set; } = PolarServer.Production;
}
