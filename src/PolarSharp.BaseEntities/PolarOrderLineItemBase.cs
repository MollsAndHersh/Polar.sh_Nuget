namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a single line item on a Polar.sh order. Carried in
/// <see cref="PolarOrderBase.Items"/>.
/// </summary>
/// <remarks>
/// Polar emits these as nested objects inside the order payload. All amounts are in cents
/// (or the smallest currency unit for non-decimal currencies).
/// </remarks>
public abstract record PolarOrderLineItemBase
{
    /// <summary>Gets the parent product identifier.</summary>
    public required string ProductId { get; init; }

    /// <summary>Gets the price identifier the line was charged at.</summary>
    public string? PriceId { get; init; }

    /// <summary>Gets the product name as displayed at order time (snapshotted; survives later product renames).</summary>
    public required string ProductName { get; init; }

    /// <summary>Gets the quantity purchased.</summary>
    public required int Quantity { get; init; }

    /// <summary>Gets the unit price in cents.</summary>
    public required int UnitAmount { get; init; }

    /// <summary>Gets the line total in cents (<see cref="Quantity"/> × <see cref="UnitAmount"/>, before line-level discounts).</summary>
    public required int LineTotal { get; init; }

    /// <summary>Gets the line-level discount applied in cents (null if no discount).</summary>
    public int? DiscountAmount { get; init; }

    /// <summary>Gets the line-level tax applied in cents (null if no tax).</summary>
    public int? TaxAmount { get; init; }
}
