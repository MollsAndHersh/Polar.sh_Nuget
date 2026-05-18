using Finbuckle.MultiTenant.Abstractions;
using PolarSharp.BaseEntities;
using PolarSharp.MultiTenant.Lifecycle;

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

    /// <summary>Gets or sets the PolarSharp-internal lifecycle status of this tenant.</summary>
    /// <value>
    /// Default: <see cref="TenantStatus.Active"/>. Distinct from the inherited
    /// <see cref="PolarTenantBase.Status"/> property (of type <see cref="PolarOrganizationStatus"/>),
    /// which reports the merchant organization's status from Polar.sh's own API. This property
    /// tracks PolarSharp-internal lifecycle state (suspended for billing dispute, soft-deleted
    /// pending retention expiry, etc.) and drives PolarSharp-side enforcement (Litestream
    /// replication exclusion, lifecycle notifications, audit logging).
    /// </value>
    /// <remarks>
    /// Stored as an integer column. Update via <see cref="ITenantStatusService"/> rather than
    /// direct mutation so MediatR lifecycle notifications fire and downstream subscribers see
    /// the change.
    /// </remarks>
    public TenantStatus LifecycleStatus { get; set; } = TenantStatus.Active;

    /// <summary>Gets or sets the email address of the tenant's site manager.</summary>
    /// <value>
    /// REQUIRED for new tenants. The destination for tenant lifecycle notifications
    /// (suspension, reactivation, deactivation, deletion) and any other admin-level messages.
    /// Must be a valid RFC 5322 email address. Stored as <c>nvarchar(320)</c>.
    /// </value>
    public string SiteManagerEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the site manager email has been verified
    /// via the verification flow (click-through link delivered to the address).
    /// </summary>
    /// <value>
    /// Default: <c>false</c>. The <see cref="ITenantStatusService"/> can be configured to
    /// refuse suspension on tenants whose email is unverified.
    /// </value>
    public bool SiteManagerEmailVerified { get; set; }

    /// <summary>Gets or sets the optional E.164-format SMS phone number for the site manager.</summary>
    /// <value>
    /// Optional. When set, lifecycle notifications can also be delivered via SMS.
    /// Format: E.164 with leading <c>+</c> (e.g. <c>+15555551234</c>). Stored as
    /// <c>nvarchar(32)</c> nullable.
    /// </value>
    public string? SiteManagerPhone { get; set; }
}
