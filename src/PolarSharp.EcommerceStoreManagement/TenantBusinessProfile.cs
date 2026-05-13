using System.Text.Json.Nodes;
using PolarSharp.BaseEntities;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement;

/// <summary>
/// The host-extended business profile for a tenant — adds local-only address, KYC fields,
/// banking-status mirrors, and per-tenant translation provider config on top of the canonical
/// Polar organization shape from <see cref="PolarTenantBase"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Local vs Polar fields:</strong> Polar's <c>OrganizationUpdate</c> accepts only
/// <c>country</c> for location and exposes <c>account_id</c> / <c>payout_account_id</c> as
/// READ-ONLY. The full street address, state, postal code, KYC details, and translation
/// config live locally — only <see cref="PolarTenantBase.Country"/>, KYC fields routed via
/// <c>OrganizationDetails</c>, and <see cref="TaxBehavior"/> are pushed to Polar.
/// </para>
/// <para>
/// <strong>Banking mirror:</strong> The inherited <see cref="PolarTenantBase.AccountId"/> and
/// <see cref="PolarTenantBase.PayoutAccountId"/> mirror Polar's <c>account_id</c> /
/// <c>payout_account_id</c> wire fields; they are read-only from Polar's perspective. The
/// host polls Polar (or subscribes to the optional account-connected webhook) and updates
/// these via the EF entity. The local <see cref="PayoutStatus"/> is computed from the two
/// account IDs and re-evaluated on each poll.
/// </para>
/// <para>
/// <strong>Translation API key is encrypted at rest.</strong>
/// <see cref="TranslationApiKeyEncrypted"/> holds the Data-Protection-API-protected
/// ciphertext; plaintext NEVER touches disk or logs. The resolver decrypts on demand when
/// invoking the translator.
/// </para>
/// </remarks>
public sealed record TenantBusinessProfile : PolarTenantBase, ITenantOwned, IFakeDataAware
{
    /// <inheritdoc/>
    string ITenantOwned.TenantId => Id;

    /// <inheritdoc/>
    public bool IsFakeData { get; init; }

    // ── Local-only extended address (Polar's Organization model has no slot for these) ──

    /// <summary>Street line 1.</summary>
    public string? StreetLine1 { get; init; }
    /// <summary>Street line 2 (apt / suite).</summary>
    public string? StreetLine2 { get; init; }
    /// <summary>City.</summary>
    public string? City { get; init; }
    /// <summary>State / province / region. Important for US/CA tax nexus.</summary>
    public string? StateOrProvince { get; init; }
    /// <summary>Postal code / ZIP.</summary>
    public string? PostalCode { get; init; }

    // ── KYC / compliance (writable via Polar OrganizationDetails) ──

    /// <summary>Short description of the products / services the tenant sells.</summary>
    public string? ProductDescription { get; init; }
    /// <summary>Free-form description of what the tenant intends to do with Polar.</summary>
    public string? IntendedUse { get; init; }
    /// <summary>Pricing models the tenant uses (one-time, subscription, both).</summary>
    public IReadOnlyList<string> PricingModels { get; init; } = [];
    /// <summary>Selling categories — Polar's category taxonomy.</summary>
    public IReadOnlyList<string> SellingCategories { get; init; } = [];
    /// <summary>Expected annual revenue (minor units).</summary>
    public long? FutureAnnualRevenue { get; init; }
    /// <summary>The platform the tenant is migrating from (if any).</summary>
    public string? SwitchingFrom { get; init; }
    /// <summary>Opaque pass-through to Polar's KYC / legal-entity backend.</summary>
    public JsonNode? LegalEntity { get; init; }

    /// <summary>Polar's tax-behaviour setting for this organization.</summary>
    public DefaultTaxBehavior TaxBehavior { get; init; } = DefaultTaxBehavior.Location;

    // ── Banking / payout mirror (READ-ONLY from Polar; updated by polling) ──
    //
    // The Stripe Connect identifier and the payout account identifier are inherited from
    // PolarTenantBase as `AccountId` and `PayoutAccountId` — they hold the same wire-format
    // values Polar emits. No need to redefine them here. The fields below add LOCAL state
    // that Polar does not track: when the host last polled, and what status the host
    // computed from the poll.

    /// <summary>Current status of the merchant's payout setup, derived locally from <see cref="PolarTenantBase.AccountId"/> and <see cref="PolarTenantBase.PayoutAccountId"/>.</summary>
    public PayoutSetupStatus PayoutStatus { get; init; } = PayoutSetupStatus.NotStarted;
    /// <summary>UTC of the most-recent payout-status poll against Polar.</summary>
    public DateTimeOffset? PayoutStatusLastCheckedAt { get; init; }

    // ── Per-tenant AI translation config (Tier 1 in the 3-tier resolution) ──

    /// <summary>Per-tenant AI translation provider. <see cref="TranslationProvider.None"/> falls back to the master/SaaS-site config (Tier 2).</summary>
    public TranslationProvider TranslationProvider { get; init; } = TranslationProvider.None;

    /// <summary>Data-Protection-API-protected API key. NEVER stored or logged in plaintext.</summary>
    public string? TranslationApiKeyEncrypted { get; init; }

    /// <summary>Provider-specific model name (e.g. <c>"claude-sonnet-4-6"</c>).</summary>
    public string? TranslationModel { get; init; }

    /// <summary>Endpoint URL — required for Azure OpenAI; optional for Gemini / Grok.</summary>
    public string? TranslationEndpoint { get; init; }

    /// <summary>The language the tenant authors content in (e.g. <c>"en-US"</c>).</summary>
    public string MasterLanguage { get; init; } = "en-US";

    /// <summary>Languages translations are produced for. The master language is implicit.</summary>
    public IReadOnlyList<string> SupportedLanguages { get; init; } = ["en-US"];

    /// <summary>When true, every product save triggers an automatic translation pass.</summary>
    public bool AutoTranslateOnSave { get; init; }

    // ── Fake data toggle ─────────────────────────────────────────────────

    /// <summary>When false, the dual global query filter hides every record where <c>IsFakeData = true</c>. Toggle change triggers a sync-to/from-Polar via the data-seeding package.</summary>
    public bool AllowFakeData { get; init; }
}
