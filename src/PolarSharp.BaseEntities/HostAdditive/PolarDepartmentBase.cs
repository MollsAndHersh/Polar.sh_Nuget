namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a top-level department — coarser-grained than a
/// <see cref="PolarCategoryBase"/> (e.g. "Clothing" → "Men's" → "Shirts" — "Clothing" is the
/// department, "Men's" might be a sub-category). Polar.sh has no native department concept;
/// departments are local-only metadata.
/// </summary>
public abstract record PolarDepartmentBase : IPolarEntity, IPolarTimestamped
{
    /// <summary>Gets the department identifier (host-assigned, typically a GUID).</summary>
    public required string Id { get; init; }

    /// <summary>Gets the department's display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the department description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the display sort order across departments (lower = earlier).</summary>
    public int SortOrder { get; init; }

    /// <summary>Gets the UTC timestamp the department was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
