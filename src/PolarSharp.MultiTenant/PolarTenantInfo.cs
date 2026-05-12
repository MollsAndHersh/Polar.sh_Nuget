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
/// <para>
/// <strong>Tenant ID convention:</strong> store a <see cref="Guid"/> string in <see cref="Id"/>
/// (e.g., <c>"3fa85f64-5717-4562-b3fc-2c963f66afa6"</c>). The computed <see cref="TenantId"/>
/// property returns the parsed <see cref="Guid"/> for use as a database primary key.
/// Finbuckle requires <see cref="Id"/> to be a <c>string</c>; this is the compatibility bridge.
/// </para>
/// </remarks>
public sealed class PolarTenantInfo : ITenantInfo
{
    /// <summary>Gets or sets the Finbuckle internal tenant ID.</summary>
    /// <value>
    /// Store a GUID string here (e.g., <c>"3fa85f64-5717-4562-b3fc-2c963f66afa6"</c>).
    /// Use the computed <see cref="TenantId"/> property to obtain the parsed <see cref="Guid"/>
    /// for database operations.
    /// </value>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets the tenant primary key as a <see cref="Guid"/>.</summary>
    /// <value>
    /// Parsed from <see cref="Id"/>. Returns <see cref="Guid.Empty"/> if <see cref="Id"/> is not
    /// a valid GUID string — ensure <see cref="Id"/> is always set to a well-formed GUID.
    /// </value>
    /// <remarks>
    /// This is a compatibility bridge: Finbuckle's <see cref="ITenantInfo"/> interface requires
    /// <see cref="Id"/> to be a <c>string</c>. Use <see cref="TenantId"/> wherever a <see cref="Guid"/>
    /// is expected (e.g., database foreign keys, DI resolution, logging scopes).
    /// </remarks>
    public Guid TenantId => Guid.TryParse(Id, out var g) ? g : Guid.Empty;

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

    /// <summary>Gets or sets the Polar organization ID associated with this tenant.</summary>
    /// <value>
    /// The <c>organization_id</c> field returned in all Polar webhook event payloads for this
    /// tenant's Polar account (e.g., <c>"org_01HXXXXXXXXXXXXXXXXXXXXXXX"</c>).
    /// Used by <see cref="PolarSharp.Webhooks.IWebhookTenantResolver"/> to route incoming webhooks to the correct
    /// tenant before HMAC verification.
    /// </value>
    /// <remarks>
    /// Find your organization ID in the Polar dashboard under Settings → Organization.
    /// Bound from <c>PolarSharp:MultiTenant:Tenants[n]:PolarOrganizationId</c>.
    /// </remarks>
    public string PolarOrganizationId { get; set; } = string.Empty;

    /// <summary>Gets or sets the per-tenant Polar webhook HMAC secret.</summary>
    /// <value>
    /// The <c>whsec_xxx</c> secret from the Polar dashboard for the webhook endpoint
    /// registered to this tenant's Polar account. When set, takes precedence over the
    /// global <c>PolarSharp:Webhooks:Secret</c> for webhooks routed to this tenant.
    /// </value>
    /// <remarks>
    /// <para>
    /// Each tenant must have a separate webhook endpoint registered in their Polar account,
    /// each with its own HMAC secret. This allows PolarSharp to verify the signature using
    /// the correct secret after routing the request to the owning tenant.
    /// </para>
    /// <para>
    /// Bound from <c>PolarSharp:MultiTenant:Tenants[n]:WebhookSecret</c>.
    /// Never log this value.
    /// </para>
    /// </remarks>
    public string? WebhookSecret { get; set; }
}
