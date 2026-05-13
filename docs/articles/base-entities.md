# Universal Domain Model — `PolarSharp.BaseEntities`

`PolarSharp.BaseEntities` defines a canonical `abstract record` hierarchy for every Polar domain entity (Customer, Order, Product, Subscription, Refund, Discount, Benefit, Checkout, LicenseKey, Address, MediaFile, Tenant) plus 6 host-additive bases for concepts Polar doesn't model natively (ShoppingCart, CartLineItem, Category, Department, InventoryRecord, Sale). Every PolarSharp dependent package AND every host application can inherit from these bases — giving **one canonical shape for every entity across the entire app stack**.

## Why this exists

Before BaseEntities, a host app had three competing shapes for an "Order":

1. The host's own `Order` class (whatever they defined)
2. Polar's Kiota-generated `Order` (inside the SDK)
3. PolarSharp's `WebhookOrderData` (in `PolarSharp.Webhooks`)

Every boundary between these shapes was a mapping bug waiting to happen. With BaseEntities, the host writes the shape **once** and inherits the canonical Polar wire format:

```csharp
public sealed record MyShopOrder : PolarOrderBase
{
    public string MyHostInternalReference { get; init; } = "";
    public bool RequiresGiftWrapping { get; init; }
}
```

Now when a Polar webhook arrives, the host's domain type IS the wire shape — zero mapping.

## Shape contract

Every base is an `abstract record` with `required init` properties:

- **Immutable** by default; `with` expressions for state transitions
- **AOT-safe** — no reflection-based instantiation
- **Required fields** mirror Polar's webhook payload exactly (nullability, types, name casing)
- **Enums** like `PolarOrderStatus`, `PolarBenefitType` use `[JsonStringEnumConverter]` so wire values match Polar's exact strings

## What's in the package

| Category | Bases |
|---|---|
| **Polar-native** (mirror webhook shapes) | `PolarTenantBase`, `PolarCustomerBase`, `PolarProductBase`, `PolarPriceBase`, `PolarOrderBase`, `PolarOrderLineItemBase`, `PolarSubscriptionBase`, `PolarRefundBase`, `PolarDiscountBase`, `PolarBenefitBase`, `PolarBenefitGrantBase`, `PolarCheckoutBase`, `PolarLicenseKeyBase`, `PolarAddressBase`, `PolarMediaFileBase` |
| **Host-additive** (Polar doesn't model these) | `PolarShoppingCartBase`, `PolarCartLineItemBase`, `PolarCategoryBase`, `PolarDepartmentBase`, `PolarInventoryRecordBase`, `PolarSaleBase` |
| **Core interfaces** | `IPolarEntity`, `IPolarTimestamped`, `IPolarMetadata`, `IPolarOrganizationScoped` |
| **Wire enums** | `PolarOrderStatus`, `PolarSubscriptionStatus`, `PolarRefundStatus`, `PolarRefundReason`, `PolarCheckoutStatus`, `PolarLicenseKeyStatus`, `PolarRecurringInterval`, `PolarTrialInterval`, `PolarOrganizationStatus`, `PolarBenefitType` |

## Version stability

The bases are **part of the v1.2.0 public API surface**. Adding a property to a base is a non-breaking change (it inherits to all derivatives). Removing or renaming a property is a major version bump.
