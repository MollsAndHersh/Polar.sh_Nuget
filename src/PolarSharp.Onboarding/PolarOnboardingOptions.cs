namespace PolarSharp.Onboarding;

/// <summary>
/// Configuration options for <c>PolarSharp.Onboarding</c>. Bound from
/// <c>PolarSharp:Onboarding</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class PolarOnboardingOptions
{
    /// <summary>The configuration root section name.</summary>
    public const string SectionName = "PolarSharp:Onboarding";

    /// <summary>The Polar environment new tenants will be provisioned against by default. Default: <see cref="PolarServer.Sandbox"/>.</summary>
    public PolarServer Server { get; set; } = PolarServer.Sandbox;

    /// <summary>OAT scope set applied when the request does not specify <see cref="ProgrammaticOnboardingRequest.Scopes"/>.</summary>
    public IReadOnlyList<string> DefaultScopes { get; set; } =
    [
        "products:write",
        "subscriptions:read",
        "orders:read",
        "customers:read",
        "webhooks:write",
        "events:read",
        "benefits:write",
        "discounts:write",
    ];

    /// <summary>Webhook event types subscribed to when the request does not specify <see cref="ProgrammaticOnboardingRequest.WebhookEvents"/>.</summary>
    public IReadOnlyList<string> DefaultWebhookEvents { get; set; } =
    [
        "order.created",
        "order.paid",
        "subscription.active",
        "subscription.canceled",
        "refund.created",
    ];

    /// <summary>OAuth-specific configuration (used only by the OAuth flow).</summary>
    public OAuthOptions OAuth { get; set; } = new();

    /// <summary>Wizard-flow configuration (opt-in; defaults to disabled for headless-only hosts).</summary>
    public WizardOptions Wizard { get; set; } = new();

    /// <summary>OAuth-flow settings.</summary>
    public sealed class OAuthOptions
    {
        /// <summary>Polar OAuth client identifier. Required when the OAuth flow is used.</summary>
        public string? ClientId { get; set; }

        /// <summary>Polar OAuth client secret. Required when the OAuth flow is used.</summary>
        public string? ClientSecret { get; set; }

        /// <summary>Callback URI registered with the Polar OAuth client. Required when the OAuth flow is used.</summary>
        public string? RedirectUri { get; set; }
    }

    /// <summary>Wizard-flow settings.</summary>
    public sealed class WizardOptions
    {
        /// <summary>When <see langword="true"/>, the wizard API (<c>IOnboardingWizard</c>) is registered. Default: <see langword="false"/>.</summary>
        public bool Enabled { get; set; }

        /// <summary>How long an in-progress wizard session lives before the cleanup service prunes it. Default: 7 days.</summary>
        public int SessionTtlDays { get; set; } = 7;

        /// <summary>When <see langword="true"/>, every wizard must complete the <c>TranslationConfig</c> step (no skipping). Default: <see langword="false"/>.</summary>
        public bool RequireTranslationStepAtOnboarding { get; set; }

        /// <summary>How often <c>OnboardingSessionExpirationCleaner</c> runs. Default: 24 hours.</summary>
        public int ExpirationCleanerIntervalHours { get; set; } = 24;
    }
}
