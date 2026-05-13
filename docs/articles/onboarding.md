# Tenant Onboarding

`PolarSharp.Onboarding` provisions new Polar merchant tenants programmatically. Two co-existing flows:

| Flow | Use when | Entry point |
|---|---|---|
| **Programmatic** | Headless / B2B — host has all values upfront | `IPolarOnboardingClient.OnboardProgrammaticallyAsync(request)` |
| **OAuth-linking** | User-consent — merchant authorises via Polar | `BuildAuthorizeUrl(...)` → redirect → `CompleteOAuthOnboardingAsync(callback)` |
| **Wizard** | Interactive UI — step-by-step with resumable sessions | `IOnboardingWizard.StartAsync()` → submit steps → `FinishAsync()` |

All three converge on the same `OnboardedTenantResult` shape: `OrganizationId`, `AccessToken`, `WebhookEndpointId`, `WebhookSecret`, `GrantedScopes`, `OnboardedAt`.

## Programmatic flow

```csharp
var result = await client.OnboardProgrammaticallyAsync(new ProgrammaticOnboardingRequest
{
    OrganizationName = "Acme",
    OrganizationSlug = "acme",
    Email = "ops@acme.example.com",
    CountryCode = "US",
    Currency = "USD",
    WebhookCallbackUrl = "https://acme.example.com/hooks/polar",
    WebhookEvents = ["order.created", "subscription.active"],
    InitialAdminEmail = "admin@acme.example.com",
});
```

Under the hood: POST `/v1/organizations/` → POST `/v1/organization-access-tokens/` → POST `/v1/webhooks/endpoints/` → persist via `IOnboardedTenantSink` → invoke post-processors (including `TenantAdminAutoProvisioningPostProcessor` when Identity is installed).

## Wizard flow

The wizard exposes step-by-step methods backed by a persistent `OnboardingSessionEntity` row. Sessions are **resumable** — a user can close the browser and pick up where they left off within the configured TTL (default 7 days). Expired sessions are auto-pruned by `OnboardingSessionExpirationCleaner` (daily).

### Step sequence

1. `SubmitCompanyBasicsAsync` — name, slug, email, country, currency, primary admin email
2. `SubmitProductTypesAsync` — what the tenant sells; answers drive conditional later steps
3. `SubmitWebhookConfigAsync` — callback URL + event subscriptions
4. `SubmitTranslationConfigAsync` (optional) — only surfaced when `ProductTypes.RequiresMultiLanguage = true`. **API key is encrypted at rest immediately via the Data Protection API** — plaintext never persisted.
5. `SubmitBankingHandoffAsync` — acknowledges that Stripe Connect linking happens out-of-band in Polar's dashboard
6. `FinishAsync` — commits; internally calls the programmatic API

### Conditional next-steps

`OnboardingStepResult.NextStep` is computed from accumulated answers. For example, when `ProductTypesStep.RequiresMultiLanguage = false`, the `TranslationConfig` step is removed from `RemainingSteps`.

## Auto-provisioned `TenantAdmin`

When `PolarSharp.MultiTenant.Identity` is installed and `TenantAdminAutoProvisioningPostProcessor` is registered (via `.AddTenantAdminAutoProvisioning()` on the Identity builder), every successful onboarding auto-creates a `TenantAdmin` membership for `InitialAdminEmail` (creating the user if needed, with a logged single-use reset token).
