namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// A single price tag attached to a storefront product or variant. Amount is recorded
/// in minor units (cents) per repo convention.
/// </summary>
/// <remarks>
/// Storefronts show prices to customers; they never compute prices client-side.
/// Server-side authoritative pricing is enforced by the
/// <c>ValidateLineItems</c> checkout pipeline stage.
/// </remarks>
public sealed record StorefrontPrice
{
    /// <summary>The price's identifier in the catalog provider.</summary>
    public required string Id { get; init; }

    /// <summary>The price in minor units (e.g. cents) of <see cref="Currency"/>.</summary>
    public required int AmountCents { get; init; }

    /// <summary>The ISO 4217 currency code (for example <c>"USD"</c>, <c>"EUR"</c>).</summary>
    public required string Currency { get; init; }

    /// <summary>
    /// True when the price is part of a recurring billing plan; false for one-off
    /// purchases.
    /// </summary>
    public bool IsRecurring { get; init; }

    /// <summary>
    /// The recurring interval token (<c>"month"</c>, <c>"year"</c>) when
    /// <see cref="IsRecurring"/> is true; <see langword="null"/> otherwise.
    /// </summary>
    public string? RecurringInterval { get; init; }

    /// <summary>
    /// Optional display label (for example "Pro plan, billed monthly"). Populated by
    /// the catalog provider for localized presentation.
    /// </summary>
    public string? DisplayLabel { get; init; }
}
