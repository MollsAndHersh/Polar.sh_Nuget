using PolarSharp;

namespace PolarSharp.Onboarding;

/// <summary>
/// Request shape for headless / programmatic tenant onboarding — every field needed to
/// provision a Polar organization, mint an OAT, and register a webhook endpoint in a single
/// API call.
/// </summary>
/// <remarks>
/// Use this from B2B / API-driven onboarding flows where the host knows all values upfront.
/// For interactive UI flows (collected step-by-step from a human), use the wizard API
/// (<c>IOnboardingWizard</c> in the <c>Wizard</c> namespace) instead — internally it
/// accumulates state and calls the programmatic path at <c>FinishAsync</c>.
/// </remarks>
public sealed record ProgrammaticOnboardingRequest
{
    /// <summary>Human-readable organization name shown in the Polar dashboard.</summary>
    public required string OrganizationName { get; init; }

    /// <summary>URL-safe slug (lowercase, hyphens, no spaces). Used in Polar checkout URLs and dashboards.</summary>
    public required string OrganizationSlug { get; init; }

    /// <summary>Primary contact email for the organization.</summary>
    public required string Email { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. <c>"US"</c>, <c>"DE"</c>). Determines Polar's tax treatment and payout rules.</summary>
    public required string CountryCode { get; init; }

    /// <summary>ISO 4217 default presentment currency (e.g. <c>"USD"</c>, <c>"EUR"</c>).</summary>
    public required string Currency { get; init; }

    /// <summary>HTTPS URL the Polar webhook will deliver events to.</summary>
    public required string WebhookCallbackUrl { get; init; }

    /// <summary>The Polar event types the webhook should subscribe to (e.g. <c>"order.created"</c>, <c>"subscription.canceled"</c>).</summary>
    public required IReadOnlyList<string> WebhookEvents { get; init; }

    /// <summary>The Polar environment to provision against. Default: <see cref="global::PolarSharp.PolarServer.Sandbox"/>.</summary>
    public PolarServer Server { get; init; } = PolarServer.Sandbox;

    /// <summary>Override the default OAT scope set. When <see langword="null"/>, the configured defaults from <see cref="PolarOnboardingOptions"/> are used.</summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>The email address of the first user to be auto-provisioned as a <c>TenantAdmin</c> via PolarSharp.MultiTenant.Identity. Optional — required only when Identity is installed.</summary>
    public string? InitialAdminEmail { get; init; }
}
