# Ecommerce Store Management

`PolarSharp.EcommerceStoreManagement` is the local catalog layer that sits between the tenant's admin UI and Polar's API. Hosts author products, variants, categories, tier-groups, benefits, discounts, and checkout links in local SQL; the publish workflow then mirrors them to Polar on demand. Polar remains the source of truth for transactions.

## What's in the box

- **Domain records** inheriting from `PolarSharp.BaseEntities` — `LocalProduct`, `LocalProductVariant`, `LocalCategory`, `LocalDepartment`, `LocalTierGroup`, `LocalPrice`, polymorphic `LocalBenefit` hierarchy (7 subtypes), `LocalDiscount`, `LocalCheckoutLinkConfig`, `TenantBusinessProfile`, `AdminAuditLogEntry`
- **Service abstractions** — `IRefundService`, `ILicenseKeyValidator`, `IInventoryUpdater`, `IPolarBusinessProfileService`, `IAuditLogActorProvider`, `IPolarCatalogPublisher`
- **Catalog cloning** — 5 cloning services with built-in duplicate prevention (see below)
- **Translation infrastructure** — `IPolarCatalogTranslator`, `ITranslationProviderResolver` (3-tier resolution: per-tenant → master → disabled), `IPolarCatalogTranslationCache` (Memory + Distributed), `IPolarCatalogReader` reassembly API
- **Refund + audit + license + inventory** service abstractions

## M:N product-to-category

A single product can live in zero, one, or **multiple** categories simultaneously:

```csharp
var product = new LocalProduct
{
    // ...
    CategoryIds = [
        new CategoryId(audioCategoryId),
        new CategoryId(bestSellersId),
        new CategoryId(mobileAccessoriesId),
    ],
};
```

Backed by the `polar_local_product_categories` join table with a unique `(product_id, category_id)` constraint.

## Catalog cloning

When a tenant admin wants to fork an existing product / category / benefit / discount / checkout link and tweak a few values, the cloning services do the right thing automatically:

```csharp
var cloned = await productCloning.CloneAsync(
    source: existingProductId,
    overrides: new CloneProductOverrides { NewMasterName = "Premium T-Shirt — Limited Edition" });
```

**Duplicate prevention is built-in:**

- Names auto-suffix with `" (Copy)"` / `" (Copy 2)"` / ... up to 100 attempts, probing the same-tenant unique index
- Discount coupon codes default to `null` on clone (avoids `(tenant_id, code)` unique-index violations; the clone becomes an automatic discount unless caller explicitly sets a new code)
- Polar-side state always reset: `PolarXxxId = null`, `LastPublishedAt = null`, `Status = Draft`
- Variants, M:N category assignments, attached benefits, per-language translation rows are all cloned with **fresh ids** inside a single `SaveChanges` transaction
- Caller can opt out of any cascade via `CloneXxxOptions` toggles

Five cloning services: `IProductCloningService`, `ICategoryCloningService`, `IBenefitCloningService`, `IDiscountCloningService`, `ICheckoutLinkCloningService`.

## Banking and payouts — why PolarSharp can't do this for you

Before a merchant can receive money, they need to connect a bank account so Polar.sh
knows where to send the payouts. **PolarSharp cannot set up this bank account
connection.** Polar's API has no endpoint for it — the merchant must do it themselves
in Polar's own admin dashboard, in a web browser.

Here's what actually happens, in plain terms:

1. The merchant goes to your application. Your admin UI shows a button or link that
   says something like "Connect your bank account to receive payouts."
2. You generate that link by calling `IPolarBusinessProfileService.BuildBankingSetupDeepLink()`.
   The link points to a specific page on Polar.sh's website.
3. The merchant clicks the link, lands on Polar's website, and walks through Polar's
   bank-account setup flow there. They may see Stripe-branded screens during this
   step — that is normal. Polar.sh uses Stripe internally to actually move money,
   but the merchant's relationship is with Polar, and your application has no
   relationship with Stripe at all.
4. When the merchant finishes the setup in Polar's dashboard, they come back to your
   application.
5. Your application calls `IPolarBusinessProfileService.RefreshPayoutStatusAsync()`
   (or relies on the optional `PayoutStatusPollerService` that does this on a
   schedule in the background). PolarSharp asks Polar's API whether the setup is
   complete and stores the answer in the local `TenantBusinessProfile`. When the
   answer is "yes," your UI can show "Payouts ready."

**Key takeaways:**

- PolarSharp **never** calls Stripe. Not once. Anywhere.
- The merchant **must** complete the bank-account setup themselves in Polar.sh's web
  dashboard. There is no programmatic shortcut. This is a Polar.sh design choice,
  not a PolarSharp limitation.
- Your application's only role is to show the link, then later check the status.

If you're building the admin UI for your tenants, surface the deep link prominently
during onboarding and keep showing it (perhaps with a "Setup incomplete" badge) until
`RefreshPayoutStatusAsync` returns `PayoutSetupStatus.Ready`.

## Three-tier translation resolution

Translation provider config resolves in priority order:

1. **Per-tenant** — `TenantBusinessProfile.TranslationProvider` + encrypted API key (BYOK; tenant pays their own AI bills)
2. **Master / SaaS-site** — `PolarSharp:EcommerceStoreManagement:Translation:*` in `appsettings.json` (SaaS host pays for all tenants who haven't set their own)
3. **Disabled** — translation features gracefully no-op; descriptions stay in master language

API keys are encrypted at rest via the ASP.NET Core Data Protection API; plaintext is never logged or returned.

## Translation storage + cache

Master-language values live in source entity rows (`local_products.master_name`). Non-master translations live in a single normalised `catalog_translations` table with a unique index on `(tenant_id, entity_type, entity_id, language, field_name)`.

`IPolarCatalogReader.GetProductLocalizedAsync(productId, "es-MX")` reassembles per-field — translation if present, master fallback if missing — and the `IPolarCatalogTranslationCache` warms the entity's full translation set on first read so subsequent language switches hit cache.
