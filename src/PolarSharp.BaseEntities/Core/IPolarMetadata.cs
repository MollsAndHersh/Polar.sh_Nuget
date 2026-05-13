namespace PolarSharp.BaseEntities;

/// <summary>
/// Marks an entity as supporting free-form key-value metadata pairs.
/// </summary>
/// <remarks>
/// Polar enforces a maximum of 50 metadata entries per entity. Keys and values are strings.
/// PolarSharp uses metadata for cross-cutting markers like
/// <c>polar_sharp_is_fake_data</c>, <c>polar_sharp_parent_id</c>,
/// <c>polar_sharp_tier_group_id</c>, etc. — all prefixed <c>polar_sharp_</c> to avoid
/// collision with host-defined metadata keys.
/// </remarks>
public interface IPolarMetadata
{
    /// <summary>Gets the entity's metadata as a read-only key-value collection.</summary>
    /// <value>
    /// Up to 50 string key-value pairs. May be empty (default-constructed entity), but never
    /// <see langword="null"/>.
    /// </value>
    IReadOnlyDictionary<string, string> Metadata { get; }
}
