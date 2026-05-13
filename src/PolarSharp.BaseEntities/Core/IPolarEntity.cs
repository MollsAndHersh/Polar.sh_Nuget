namespace PolarSharp.BaseEntities;

/// <summary>
/// Marks a record/class as a Polar.sh entity that has a stable identifier.
/// </summary>
/// <remarks>
/// Implemented by every <c>PolarXxxBase</c> in this package. Hosts inheriting from those
/// bases automatically satisfy this interface.
/// </remarks>
public interface IPolarEntity
{
    /// <summary>Gets the Polar.sh-assigned identifier for the entity.</summary>
    /// <value>
    /// A stable string id assigned by Polar (e.g. <c>"prod_xxx"</c>, <c>"order_yyy"</c>,
    /// <c>"cus_zzz"</c>). Conventionally a GUID-like string from Polar.
    /// </value>
    string Id { get; }
}
