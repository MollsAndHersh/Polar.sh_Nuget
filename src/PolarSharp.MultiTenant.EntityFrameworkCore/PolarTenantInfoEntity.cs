using Finbuckle.MultiTenant.Abstractions;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// The EF Core-mapped representation of a PolarSharp tenant.
/// </summary>
/// <remarks>
/// <para>
/// Persisted by <see cref="PolarTenantDbContext"/> in the tenant registry. Implements both
/// Finbuckle's <see cref="ITenantInfo"/> contract (string-typed <see cref="Id"/> property,
/// required by the Finbuckle store interface) and PolarSharp's GUID-first convention
/// (<see cref="TenantId"/> property returns the parsed <see cref="System.Guid"/>).
/// </para>
/// <para>
/// The <see cref="PolarAccessToken"/> field is treated as sensitive and is masked in all log
/// output via <c>PolarSharp.Logging.RedactPii</c> (default on in Production).
/// </para>
/// </remarks>
public sealed class PolarTenantInfoEntity : ITenantInfo
{
    /// <summary>Gets or sets the tenant's GUID identifier as a canonical string.</summary>
    /// <value>A canonical Guid string (e.g. <c>"3fa85f64-5717-4562-b3fc-2c963f66afa6"</c>).</value>
    /// <remarks>
    /// Required by Finbuckle's <see cref="ITenantInfo"/> contract. PolarSharp generates GUID-formed
    /// identifiers exclusively; the EF column type is <see cref="System.Guid"/> with a
    /// string-conversion at the API boundary.
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets the tenant's identifier as a typed <see cref="System.Guid"/>.</summary>
    /// <value>
    /// The parsed Guid from <see cref="Id"/>. Returns <see cref="System.Guid.Empty"/> when
    /// <see cref="Id"/> is unset or malformed — ensure every persisted row carries a
    /// well-formed Guid string.
    /// </value>
    public Guid TenantId
        => Guid.TryParse(Id, out var g) ? g : Guid.Empty;

    /// <summary>
    /// Gets or sets the human-readable identifier Finbuckle uses to resolve the tenant from a
    /// header / route / hostname / claim.
    /// </summary>
    /// <value>
    /// A URL-safe slug (e.g. <c>"acme-corp"</c>). Must be unique per tenant registry.
    /// </value>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant's display name for admin UIs and audit log entries.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the Polar.sh Organization Access Token used to authenticate this tenant's API calls.</summary>
    /// <value>
    /// A Polar OAT string (e.g. <c>"polar_oat_xxx"</c>). Treated as sensitive — masked in all
    /// log scopes. Rotatable via <c>IOptionsMonitor</c> hot-reload semantics (no app restart).
    /// </value>
    public string PolarAccessToken { get; set; } = "";

    /// <summary>Gets or sets the Polar.sh server environment this tenant targets.</summary>
    /// <value>
    /// <see cref="PolarServer.Production"/>, <see cref="PolarServer.Sandbox"/>, or
    /// <see cref="PolarServer.Custom"/>. Determines the base URL used for outbound API calls.
    /// </value>
    public PolarServer Server { get; set; } = PolarServer.Production;

    /// <summary>Gets or sets the UTC timestamp the tenant was onboarded.</summary>
    public DateTimeOffset OnboardedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the Polar webhook endpoint identifier for this tenant (populated by onboarding).</summary>
    /// <value>The Polar-assigned webhook endpoint ID (e.g. <c>"webhook_endpoint_xxx"</c>), or <see langword="null"/> if webhooks are not yet configured.</value>
    public string? WebhookEndpointId { get; set; }

    /// <summary>Gets or sets the HMAC signing secret for verifying this tenant's webhook deliveries.</summary>
    /// <value>
    /// The Polar-generated webhook signing secret (e.g. <c>"whsec_xxx"</c>). Treated as sensitive — never logged.
    /// </value>
    public string WebhookSecret { get; set; } = "";
}
