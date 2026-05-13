# PolarSharp.BaseEntities

Universal abstract record bases for every canonical Polar.sh domain entity. The bottom of the PolarSharp dependency tree — zero external NuGet dependencies, AOT-safe, targets `net10.0`.

## Why this exists

Without `PolarSharp.BaseEntities`, a host application typically had three different shapes for "an order":

- The host's own `Order` class
- Polar's Kiota-generated `Order` (in `PolarSharp.Generated.Models`)
- PolarSharp.Webhooks' `WebhookOrderData` (sealed record in v1.1.0)

Every boundary between them required mapping. With BaseEntities, a host writes once:

```csharp
public sealed record MyShopOrder : PolarOrderBase
{
    // Inherits Id, Status, Number, Amount, Currency, BillingReason, CustomerId,
    // OrganizationId, SubscriptionId, CreatedAt, ... from PolarOrderBase — match Polar's wire format
    public string MyHostInternalReference { get; init; } = "";
    public bool RequiresGiftWrapping { get; init; }
}
```

When a webhook arrives in v1.2.0, `WebhookOrderData` IS a `PolarOrderBase` (the v1.1.0 sealed records are un-sealed and refactored to inherit). The host can directly use the data with zero translation.

## Install

```bash
dotnet add package PolarSharp.BaseEntities
```

(In practice you'll get this transitively when installing any other PolarSharp v1.2.0+ package.)

## What's in this package

**15 Polar-native bases** (mirror Polar's webhook wire format exactly):

- `PolarTenantBase` — Polar.sh organization (a merchant tenant)
- `PolarCustomerBase` — Polar payer (distinct from the SaaS host's own user)
- `PolarProductBase` — one-time or recurring product
- `PolarPriceBase` — fixed / custom / free / metered / seat-tiered pricing
- `PolarOrderBase` — order header
- `PolarOrderLineItemBase` — line-item detail
- `PolarSubscriptionBase` — recurring billing record
- `PolarRefundBase` — full or partial refund
- `PolarDiscountBase` — coupon or automatic discount
- `PolarBenefitBase` — entitlement granted on purchase
- `PolarBenefitGrantBase` — a customer's claim on a benefit
- `PolarCheckoutBase` — checkout session
- `PolarLicenseKeyBase` — license key issuance
- `PolarAddressBase` — postal address
- `PolarMediaFileBase` — media file metadata

**6 host-additive bases** (concepts Polar does NOT model — every host can inherit):

- `PolarShoppingCartBase` — pre-checkout cart
- `PolarCartLineItemBase` — cart line item
- `PolarCategoryBase` — fine-grained product grouping
- `PolarDepartmentBase` — top-level grouping (e.g. "Clothing" → "Men's" → "Shirts")
- `PolarInventoryRecordBase` — per-SKU on-hand count + low-stock thresholding
- `PolarSaleBase` — time-bounded promotional discount campaign (extends `PolarDiscountBase`)

**10 Polar wire enums** (`[JsonStringEnumConverter]`-backed, exact wire-format strings):

- `PolarOrderStatus`, `PolarSubscriptionStatus`, `PolarRefundStatus`, `PolarRefundReason`
- `PolarCheckoutStatus`, `PolarLicenseKeyStatus`
- `PolarRecurringInterval`, `PolarTrialInterval`
- `PolarOrganizationStatus`, `PolarBenefitType`

**4 core interfaces** (composable contracts):

- `IPolarEntity` — has an `Id`
- `IPolarTimestamped` — has `CreatedAt`
- `IPolarMetadata` — has free-form `Metadata` key-value pairs (≤50 entries per Polar's limits)
- `IPolarOrganizationScoped` — belongs to an `OrganizationId`

## License

MIT — see [LICENSE](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/LICENSE).
