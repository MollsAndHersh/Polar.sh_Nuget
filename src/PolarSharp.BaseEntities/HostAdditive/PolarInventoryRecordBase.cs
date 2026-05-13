namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a single SKU's on-hand inventory record. Polar.sh has no native
/// inventory tracking; this base belongs to the host-additive group. PolarSharp's
/// inventory-auto-sync feature reads these records and pushes <c>is_archived</c> to Polar
/// when a SKU crosses the zero-on-hand boundary.
/// </summary>
public abstract record PolarInventoryRecordBase : IPolarTimestamped
{
    /// <summary>Gets the SKU or variant identifier the inventory record applies to.</summary>
    public required string SkuOrVariantId { get; init; }

    /// <summary>Gets the current on-hand count.</summary>
    public required int OnHandCount { get; init; }

    /// <summary>Gets the threshold at which the record is considered "low stock" (null = no threshold configured).</summary>
    public int? LowThreshold { get; init; }

    /// <summary>Gets a value indicating whether the SKU is currently out of stock.</summary>
    public bool IsOutOfStock => OnHandCount <= 0;

    /// <summary>Gets a value indicating whether the SKU is at or below its low-stock threshold (always false when no threshold is configured).</summary>
    public bool IsLowStock => LowThreshold.HasValue && OnHandCount <= LowThreshold.Value;

    /// <summary>Gets the UTC timestamp the record was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp the on-hand count was last changed.</summary>
    public DateTimeOffset? LastChangedAt { get; init; }
}
