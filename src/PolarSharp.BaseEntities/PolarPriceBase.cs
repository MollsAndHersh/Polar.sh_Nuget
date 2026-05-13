namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh product price. Polar supports five pricing models —
/// <c>fixed</c>, <c>custom</c> (pay-what-you-want), <c>free</c>, <c>metered_unit</c>, and
/// <c>seat_based</c> with graduated/volume tiers.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="AmountType"/> field discriminates which pricing model applies. The relevant
/// amount fields (<see cref="PriceAmount"/>, <see cref="MinimumAmount"/>,
/// <see cref="PresetAmount"/>, <see cref="MaximumAmount"/>, <see cref="MeterId"/>) carry the
/// model-specific configuration; non-applicable fields are <see langword="null"/>.
/// </para>
/// <para>
/// All currency amounts are in <strong>cents</strong> (or the smallest currency unit for
/// non-decimal currencies — JPY = yen, KRW = won, etc.).
/// </para>
/// </remarks>
public abstract record PolarPriceBase : IPolarEntity, IPolarTimestamped
{
    /// <summary>Gets the Polar.sh price identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the parent product identifier.</summary>
    public required string ProductId { get; init; }

    /// <summary>Gets the ISO 4217 currency code (e.g. "USD", "EUR"). Null for free pricing.</summary>
    public string? Currency { get; init; }

    /// <summary>Gets the fixed price in cents. Used when <see cref="AmountType"/> is "fixed".</summary>
    public int? PriceAmount { get; init; }

    /// <summary>Gets the minimum amount the customer can pay in cents. Used when <see cref="AmountType"/> is "custom".</summary>
    public int? MinimumAmount { get; init; }

    /// <summary>Gets the suggested default amount in cents. Used when <see cref="AmountType"/> is "custom".</summary>
    public int? PresetAmount { get; init; }

    /// <summary>Gets the maximum amount the customer can pay in cents. Used when <see cref="AmountType"/> is "custom".</summary>
    public int? MaximumAmount { get; init; }

    /// <summary>Gets the pricing model discriminator. One of "fixed", "custom", "free", "metered_unit", "seat_based".</summary>
    public string? AmountType { get; init; }

    /// <summary>Gets the meter identifier this price reads usage from. Used when <see cref="AmountType"/> is "metered_unit".</summary>
    public string? MeterId { get; init; }

    /// <summary>Gets a value indicating whether the price has been archived (hidden from new checkouts).</summary>
    public bool IsArchived { get; init; }

    /// <summary>Gets the UTC timestamp the price was created in Polar.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp the price was last modified in Polar.</summary>
    public DateTimeOffset? ModifiedAt { get; init; }
}
