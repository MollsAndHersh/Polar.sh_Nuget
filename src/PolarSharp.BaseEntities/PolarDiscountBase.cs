namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh discount (coupon code or automatic discount). Polar
/// supports four discount variants: fixed-once, fixed-repeat, percentage-once, percentage-repeat
/// — discriminated by <see cref="Type"/> + <see cref="Duration"/>.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="Type"/> is "fixed", <see cref="AmountOff"/> + <see cref="Currency"/> apply.
/// When <see cref="Type"/> is "percentage", <see cref="PercentageOff"/> applies (0–100).
/// </para>
/// <para>
/// <see cref="PolarSaleBase"/> extends this base to add campaign-style metadata (banner image,
/// campaign name) for time-bounded promotional discounts.
/// </para>
/// </remarks>
public abstract record PolarDiscountBase : IPolarEntity, IPolarTimestamped, IPolarOrganizationScoped
{
    /// <summary>Gets the Polar.sh discount identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the coupon code customers enter at checkout. Null for automatic (no-code) discounts.</summary>
    public string? Code { get; init; }

    /// <summary>Gets the human-readable name of the discount (admin display).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the discount type — "fixed" or "percentage".</summary>
    public required string Type { get; init; }

    /// <summary>Gets the fixed discount amount in cents (when <see cref="Type"/> is "fixed").</summary>
    public int? AmountOff { get; init; }

    /// <summary>Gets the percentage discount (0–100, when <see cref="Type"/> is "percentage").</summary>
    public decimal? PercentageOff { get; init; }

    /// <summary>Gets the ISO 4217 currency code (required when <see cref="Type"/> is "fixed").</summary>
    public string? Currency { get; init; }

    /// <summary>Gets the duration semantics — "once", "forever", or "repeating".</summary>
    public string? Duration { get; init; }

    /// <summary>Gets the count of recurring billing cycles the discount applies to (when <see cref="Duration"/> is "repeating").</summary>
    public int? DurationInMonths { get; init; }

    /// <summary>Gets the UTC timestamp the discount becomes valid (null = always-valid from creation).</summary>
    public DateTimeOffset? StartsAt { get; init; }

    /// <summary>Gets the UTC timestamp the discount expires (null = never expires).</summary>
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>Gets the maximum number of times the discount can be redeemed (null = unlimited).</summary>
    public int? MaxRedemptions { get; init; }

    /// <summary>Gets the product identifiers the discount applies to (empty = applies to all products).</summary>
    public IReadOnlyList<string> ApplicableProductIds { get; init; } = [];

    /// <summary>Gets the Polar organization the discount belongs to.</summary>
    public required string OrganizationId { get; init; }

    /// <summary>Gets the UTC timestamp the discount was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
