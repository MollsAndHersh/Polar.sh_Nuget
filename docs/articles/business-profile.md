# Business profile + Stripe-Connect handoff

`TenantBusinessProfile` is the tenant's business identity in PolarSharp — business address, KYC fields, tax behaviour, payout setup status, and per-tenant translation provider config. `IPolarBusinessProfileService` is the read/write surface.

## Why local, when Polar exposes Organization?

Polar's `Organization` resource exposes only `Country` for location, no street/city/state/postal. PolarSharp keeps the full address locally (`TenantBusinessProfile.StreetLine1/City/StateOrProvince/PostalCode`) and submits only what Polar's `OrganizationUpdate` accepts. The local copy is the source of truth for tax-nexus determination, invoices, and merchant dashboards; the Polar copy is the minimum the payment platform needs.

## Service surface

```csharp
public interface IPolarBusinessProfileService
{
    Task<Result<TenantBusinessProfile, PolarError>> GetAsync(TenantId tenantId, CancellationToken ct);

    /// <summary>Persists locally; pushes writable fields (country, currency, tax behaviour, OrganizationDetails) to Polar.</summary>
    Task<Result<Unit, PolarError>> SaveAsync(TenantBusinessProfile profile, CancellationToken ct);

    /// <summary>Returns a deep-link URL to Polar's dashboard for the merchant to complete Stripe Connect onboarding.</summary>
    Uri BuildBankingSetupDeepLink(TenantId tenantId);

    /// <summary>Polls Polar's read-only account_id / payout_account_id and updates the local PayoutStatus.</summary>
    Task<Result<PayoutSetupStatus, PolarError>> RefreshPayoutStatusAsync(TenantId tenantId, CancellationToken ct);
}
```

## Stripe Connect deep-link — the critical framing

**PolarSharp does NOT talk to Stripe. Ever. Anywhere.** Polar's API exposes no programmatic Stripe-Connect flow — `Organization.account_id` / `Organization.payout_account_id` are **read-only** in Polar's API. Merchants complete Connect onboarding through Polar's own web dashboard.

`BuildBankingSetupDeepLink(tenantId)` returns the URL the host's UI should open in a new tab: `https://polar.sh/dashboard/{organization-slug}/settings/banking` (or the sandbox equivalent). The merchant completes the Stripe screens inside Polar's dashboard. PolarSharp's only role is delivering the link.

`PayoutStatusPollerService` (`IHostedService`) polls `RefreshPayoutStatusAsync` on a configurable schedule (default 5 min for tenants whose status is `NotStarted` or `InProgress`; never for `Ready`). When the status transitions to `Ready`, the service emits an event the host can wire to "Your bank account is connected and you can now receive payouts."

## KYC / OrganizationDetails fields

`SaveAsync` pushes the KYC subset Polar's `OrganizationDetails` PATCH endpoint accepts: `FutureAnnualRevenue`, `IntendedUse`, `PricingModels`, `ProductDescription`, `SellingCategories`, `SwitchingFrom`, plus the opaque `LegalEntity` pass-through. PolarSharp does not interpret these fields — Polar's compliance backend does.

## Per-tenant translation provider config

`TenantBusinessProfile` also carries `TranslationProvider` / `TranslationApiKeyEncrypted` / `TranslationModel` / `MasterLanguage` / `SupportedLanguages` — these power the 3-tier translation provider resolution. The encrypted API key is protected via ASP.NET Core Data Protection API; it never appears in logs, audit-log entries, or session JSON in plaintext. See [Ecommerce Store Management](ecommerce-catalog.md) for the resolution rules.

## v2.0 deferral

`IPolarOrganizationsApi` (the HTTP boundary) is currently a deferred stub (`PolarClientOrganizationsApi`, tracked as TASK-V20-004). `SaveAsync` persists locally today but doesn't yet push to Polar's `OrganizationUpdate` endpoint; `RefreshPayoutStatusAsync` returns the last persisted status without a live poll. Hosts implementing custom `IPolarOrganizationsApi` against the live Polar API get full functionality.
