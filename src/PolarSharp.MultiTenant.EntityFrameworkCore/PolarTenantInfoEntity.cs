using Finbuckle.MultiTenant.Abstractions;
using PolarSharp.BaseEntities;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// The EF Core-mapped representation of a PolarSharp tenant.
/// </summary>
/// <remarks>
/// <para>
/// Inherits from <see cref="PolarTenantBase"/> so the entity carries Polar's wire-format
/// tenant shape (Id, Name, Slug, Country, Email, Status, AccountId, PayoutAccountId,
/// CreatedAt, ModifiedAt) AND adds Finbuckle's <see cref="ITenantInfo"/> contract
/// (<see cref="Identifier"/>) plus PolarSharp-internal fields the host doesn't expose
/// publicly (<see cref="PolarAccessToken"/>, <see cref="Server"/>, <see cref="WebhookEndpointId"/>,
/// <see cref="WebhookSecret"/>).
/// </para>
/// <para>
/// The <see cref="PolarAccessToken"/> field is treated as sensitive and is masked in all log
/// output via <c>PolarSharp.Logging.RedactPii</c> (default on in Production). Same for
/// <see cref="WebhookSecret"/>.
/// </para>
/// </remarks>
public sealed record PolarTenantInfoEntity : PolarTenantBase, ITenantInfo
{
    /// <summary>Gets the tenant's identifier as a typed <see cref="System.Guid"/>.</summary>
    /// <value>
    /// The parsed Guid from <see cref="PolarTenantBase.Id"/>. Returns <see cref="System.Guid.Empty"/>
    /// when the Id is unset or malformed — ensure every persisted row carries a well-formed
    /// Guid string.
    /// </value>
    public Guid TenantId
        => Guid.TryParse(Id, out var g) ? g : Guid.Empty;

    /// <summary>
    /// Gets or sets the human-readable identifier Finbuckle uses to resolve the tenant from a
    /// header / route / hostname / claim.
    /// </summary>
    /// <value>A URL-safe slug (e.g. <c>"acme-corp"</c>). Must be unique per tenant registry.</value>
    /// <remarks>
    /// Required by Finbuckle's <see cref="ITenantInfo"/> contract — distinct from Polar's
    /// own <see cref="PolarTenantBase.Slug"/> field, though they're often the same string in
    /// practice.
    /// </remarks>
    public string Identifier { get; set; } = string.Empty;

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

    /// <summary>Gets or sets the Polar webhook endpoint identifier for this tenant (populated by onboarding).</summary>
    /// <value>The Polar-assigned webhook endpoint ID (e.g. <c>"webhook_endpoint_xxx"</c>), or <see langword="null"/> if webhooks are not yet configured.</value>
    public string? WebhookEndpointId { get; set; }

    /// <summary>Gets or sets the HMAC signing secret for verifying this tenant's webhook deliveries.</summary>
    /// <value>
    /// The Polar-generated webhook signing secret (e.g. <c>"whsec_xxx"</c>). Treated as sensitive — never logged.
    /// </value>
    public string WebhookSecret { get; set; } = "";
}
