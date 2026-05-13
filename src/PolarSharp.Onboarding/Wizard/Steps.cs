namespace PolarSharp.Onboarding.Wizard;

/// <summary>Step 1 — basic company / organization details.</summary>
public sealed record CompanyBasicsStep
{
    /// <summary>Human-readable organization name.</summary>
    public required string OrganizationName { get; init; }
    /// <summary>URL-safe slug.</summary>
    public required string OrganizationSlug { get; init; }
    /// <summary>Primary contact email.</summary>
    public required string Email { get; init; }
    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public required string CountryCode { get; init; }
    /// <summary>ISO 4217 default presentment currency.</summary>
    public required string Currency { get; init; }
    /// <summary>Email of the first user to be auto-provisioned as TenantAdmin.</summary>
    public required string PrimaryAdminEmail { get; init; }
    /// <summary>Optional admin full name.</summary>
    public string? PrimaryAdminFullName { get; init; }
}

/// <summary>Step 2 — what the tenant sells. The answers drive conditional later steps.</summary>
public sealed record ProductTypesStep
{
    /// <summary>The tenant sells physical goods that need shipping.</summary>
    public required bool SellsPhysicalGoods { get; init; }
    /// <summary>The tenant sells downloadable digital goods.</summary>
    public required bool SellsDigitalGoods { get; init; }
    /// <summary>The tenant sells services / consulting.</summary>
    public required bool SellsServices { get; init; }
    /// <summary>The tenant sells recurring subscriptions.</summary>
    public required bool SellsSubscriptions { get; init; }
    /// <summary>The tenant needs license-key generation.</summary>
    public required bool RequiresLicenseKeys { get; init; }
    /// <summary>The tenant needs file-download benefits.</summary>
    public required bool RequiresFileDownloads { get; init; }
    /// <summary>The tenant needs multi-language catalog descriptions — toggles whether the <see cref="OnboardingStepKind.TranslationConfig"/> step is surfaced.</summary>
    public required bool RequiresMultiLanguage { get; init; }
}

/// <summary>Step 3 — Polar webhook callback configuration.</summary>
public sealed record WebhookConfigStep
{
    /// <summary>HTTPS URL Polar will deliver webhooks to.</summary>
    public required string CallbackUrl { get; init; }
    /// <summary>Polar event types to subscribe to.</summary>
    public required IReadOnlyList<string> EventTypes { get; init; }
}

/// <summary>
/// Optional step — AI translation provider per-tenant configuration. Only surfaced when
/// <see cref="ProductTypesStep.RequiresMultiLanguage"/> = <see langword="true"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Critical security note:</strong> <see cref="ApiKeyPlaintext"/> is encrypted via the
/// ASP.NET Core Data Protection API IMMEDIATELY in <c>SubmitTranslationConfigAsync</c>; only
/// the encrypted form is persisted on the session row. The plaintext is cleared from session
/// memory after persistence and never logged.
/// </para>
/// </remarks>
public sealed record TranslationConfigStep
{
    /// <summary>The translation provider name (e.g. <c>"Anthropic"</c>, <c>"OpenAI"</c>, <c>"AzureOpenAI"</c>, <c>"Gemini"</c>, <c>"Grok"</c>, or <c>"None"</c> to skip).</summary>
    public required string Provider { get; init; }
    /// <summary>The raw API key. Encrypted at rest immediately upon receipt; never logged.</summary>
    public string? ApiKeyPlaintext { get; init; }
    /// <summary>Provider-specific model name (e.g. <c>"claude-sonnet-4-6"</c>, <c>"gpt-4o"</c>).</summary>
    public string? Model { get; init; }
    /// <summary>Endpoint URL (required for Azure OpenAI; optional for others).</summary>
    public string? Endpoint { get; init; }
    /// <summary>The master language the tenant authors content in (e.g. <c>"en-US"</c>).</summary>
    public string MasterLanguage { get; init; } = "en-US";
    /// <summary>Languages translations are produced for. The master language is implicit.</summary>
    public IReadOnlyList<string> SupportedLanguages { get; init; } = [];
    /// <summary>When <see langword="true"/>, translations run automatically on every product save.</summary>
    public bool AutoTranslateOnSave { get; init; }
}

/// <summary>Final step — acknowledges that Stripe Connect onboarding happens out-of-band in the Polar dashboard.</summary>
public sealed record BankingHandoffStep
{
    /// <summary>The host's UI confirms it explained the dashboard redirect to the user.</summary>
    public required bool AcknowledgedDashboardRedirect { get; init; }
    /// <summary>When <see langword="true"/>, the tenant can complete banking after onboarding completes (banking status remains <c>NotStarted</c> at finish).</summary>
    public bool DeferToLater { get; init; }
}
