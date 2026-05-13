using PolarSharp.BaseEntities;

namespace PolarSharp.EcommerceStoreManagement;

/// <summary>
/// Pricing definition attached to a <see cref="LocalProduct"/> or a variant. Expands to one
/// or more Polar <c>ProductPrice</c> entries at publish time depending on
/// <see cref="Kind"/>.
/// </summary>
public sealed record LocalPrice
{
    /// <summary>Which Polar price shape this maps to.</summary>
    public required PriceKind Kind { get; init; }

    /// <summary>The flat amount (minor units, e.g. cents) when <see cref="Kind"/> is <see cref="PriceKind.Fixed"/>.</summary>
    public int? Amount { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>Lower bound when <see cref="Kind"/> is <see cref="PriceKind.Custom"/>.</summary>
    public int? MinAmount { get; init; }

    /// <summary>Suggested amount when <see cref="Kind"/> is <see cref="PriceKind.Custom"/>.</summary>
    public int? PresetAmount { get; init; }

    /// <summary>Upper bound when <see cref="Kind"/> is <see cref="PriceKind.Custom"/>.</summary>
    public int? MaxAmount { get; init; }

    /// <summary>Seat-volume tiers when <see cref="Kind"/> is <see cref="PriceKind.SeatBased"/>.</summary>
    public IReadOnlyList<SeatTier> SeatTiers { get; init; } = [];

    /// <summary>The Polar meter id this price consumes against when <see cref="Kind"/> is <see cref="PriceKind.MeteredUnit"/>.</summary>
    public string? MeterId { get; init; }

    /// <summary>True when this is a recurring price. Drives whether Polar publishes the product as one-time or subscription.</summary>
    public bool IsRecurring { get; init; }

    /// <summary>Billing interval (when <see cref="IsRecurring"/> is true).</summary>
    public PolarRecurringInterval RecurringInterval { get; init; } = PolarRecurringInterval.None;

    /// <summary>Trial duration in <see cref="TrialInterval"/> units when offering a free trial.</summary>
    public int? TrialIntervalCount { get; init; }

    /// <summary>Trial duration unit (day / week / month).</summary>
    public PolarTrialInterval? TrialInterval { get; init; }
}

/// <summary>One band of a seat-volume price ladder. Inclusive lower bound; exclusive upper bound (null = unbounded).</summary>
/// <param name="MinSeats">Lower bound, inclusive. The first tier starts at 1.</param>
/// <param name="MaxSeats">Upper bound, exclusive. <see langword="null"/> means the tier extends to infinity.</param>
/// <param name="PricePerSeat">Per-seat price in minor currency units.</param>
public sealed record SeatTier(int MinSeats, int? MaxSeats, int PricePerSeat);
