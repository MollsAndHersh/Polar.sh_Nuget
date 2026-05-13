namespace PolarSharp.BaseEntities;

/// <summary>
/// Lifecycle status of a host-defined Sale campaign (computed from <c>StartsAt</c> /
/// <c>EndsAt</c> bounds). Polar.sh has no native Sale concept — this enum belongs to the
/// host-additive base <see cref="PolarSaleBase"/>.
/// </summary>
public enum PolarSaleStatus
{
    /// <summary>The sale's <c>StartsAt</c> is in the future; not yet active.</summary>
    Pending,

    /// <summary>The sale is currently in-progress (now is between <c>StartsAt</c> and <c>EndsAt</c>).</summary>
    Active,

    /// <summary>The sale's <c>EndsAt</c> is in the past; campaign has concluded.</summary>
    Ended,
}
