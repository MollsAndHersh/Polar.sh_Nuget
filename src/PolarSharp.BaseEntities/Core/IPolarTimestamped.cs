namespace PolarSharp.BaseEntities;

/// <summary>
/// Marks an entity as carrying a UTC creation timestamp from Polar.sh.
/// </summary>
/// <remarks>
/// Polar emits this on every entity in webhook payloads as the <c>created_at</c> field,
/// always in ISO 8601 UTC format.
/// </remarks>
public interface IPolarTimestamped
{
    /// <summary>Gets the UTC timestamp the entity was created in Polar.sh.</summary>
    /// <value>The <c>created_at</c> field from Polar's wire format.</value>
    DateTimeOffset CreatedAt { get; }
}
