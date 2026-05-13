namespace PolarSharp.Onboarding.Wizard;

/// <summary>The discrete steps a wizard session can be at.</summary>
public enum OnboardingStepKind
{
    /// <summary>Step 1 — basic company/org details (name, slug, email, country, currency).</summary>
    CompanyBasics,

    /// <summary>Step 2 — what the tenant sells (physical/digital/services/subscriptions, language support).</summary>
    ProductTypes,

    /// <summary>Step 3 — Polar webhook callback URL + event subscriptions.</summary>
    WebhookConfig,

    /// <summary>Optional — AI translation provider configuration (skipped when <c>ProductTypes.RequiresMultiLanguage</c> is false).</summary>
    TranslationConfig,

    /// <summary>Final pre-commit step — acknowledges that Stripe Connect onboarding happens out-of-band in the Polar dashboard.</summary>
    BankingHandoff,
}
