namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a single line in a host-side shopping cart (pre-checkout). Polar.sh
/// has no native cart concept — this base belongs to the host-additive group, alongside
/// <see cref="PolarShoppingCartBase"/>.
/// </summary>
/// <remarks>
/// Snapshots unit price at add-to-cart time so that a price change between add-to-cart and
/// checkout doesn't surprise the customer.
/// </remarks>
public abstract record PolarCartLineItemBase
{
    /// <summary>Gets the product identifier the line refers to.</summary>
    public required string ProductId { get; init; }

    /// <summary>Gets the variant identifier (when the host models product variants locally; Polar publishes variants as separate products).</summary>
    public string? VariantId { get; init; }

    /// <summary>Gets the SKU (stock-keeping unit) for fulfillment / inventory.</summary>
    public string? Sku { get; init; }

    /// <summary>Gets the product name as displayed when the line was added (snapshotted).</summary>
    public required string ProductName { get; init; }

    /// <summary>Gets the quantity of units.</summary>
    public required int Quantity { get; init; }

    /// <summary>Gets the unit price in cents (snapshotted at add-to-cart time).</summary>
    public required int UnitAmount { get; init; }

    /// <summary>Gets the line total in cents (<see cref="Quantity"/> × <see cref="UnitAmount"/>).</summary>
    public required int LineTotal { get; init; }
}
