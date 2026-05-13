using PolarSharp;

namespace PolarSharp.Onboarding;

/// <summary>
/// The fully-populated outcome of a successful onboarding flow — everything a host needs to
/// start serving the newly-onboarded tenant: Polar organization id, organization access token
/// (OAT), webhook secret, and tenant identifier.
/// </summary>
/// <remarks>
/// <para>
/// Returned by both <see cref="IPolarOnboardingClient.OnboardProgrammaticallyAsync"/> and
/// the wizard API's <c>FinishAsync</c>. The host typically persists this
/// record via an <see cref="IOnboardedTenantSink"/> implementation — the bundled
/// <c>EfMultiTenantStoreSink</c> writes it directly into the
/// <c>PolarSharp.MultiTenant.EntityFrameworkCore</c> tenant store.
/// </para>
/// <para>
/// <strong>Secret handling:</strong> <see cref="AccessToken"/> and <see cref="WebhookSecret"/>
/// are raw secrets — the host is responsible for encrypting them at rest (PolarSharp's tenant
/// store does this automatically when used with the EF sink). They must never be logged or
/// returned in HTTP responses; the PolarSharp logging redactor masks both.
/// </para>
/// </remarks>
public sealed record OnboardedTenantResult
{
    /// <summary>The PolarSharp tenant identifier — typically equal to <see cref="OrganizationId"/> for 1:1 mapping; hosts may override.</summary>
    public required string TenantId { get; init; }

    /// <summary>The Polar organization id (<c>org_xxx</c> format).</summary>
    public required string OrganizationId { get; init; }

    /// <summary>The Polar organization slug (URL-safe identifier).</summary>
    public required string OrganizationSlug { get; init; }

    /// <summary>The Organization Access Token (OAT) — used to authenticate all subsequent Polar API calls on behalf of this tenant.</summary>
    public required string AccessToken { get; init; }

    /// <summary>The Polar webhook endpoint id (<c>whep_xxx</c> format) — identifies the registered HTTPS endpoint that will receive event deliveries.</summary>
    public required string WebhookEndpointId { get; init; }

    /// <summary>The shared secret Polar uses to HMAC-sign webhook deliveries. Verify every incoming webhook against this value.</summary>
    public required string WebhookSecret { get; init; }

    /// <summary>The Polar environment this tenant was provisioned against.</summary>
    public required PolarServer Server { get; init; }

    /// <summary>The OAT scopes granted at provisioning time.</summary>
    public IReadOnlyList<string> GrantedScopes { get; init; } = [];

    /// <summary>UTC timestamp of the successful onboarding.</summary>
    public required DateTimeOffset OnboardedAt { get; init; }

    /// <summary>The email address of the user who is to become the initial <c>TenantAdmin</c> for this tenant (when Identity is installed).</summary>
    public string? InitialAdminEmail { get; init; }
}
