namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a product/service category — a finer-grained grouping than a
/// <see cref="PolarDepartmentBase"/>. Polar.sh has no native category concept; categories
/// are local-only metadata used to organize products in the host's admin UI and storefront.
/// </summary>
/// <remarks>
/// Categories may be hierarchical via <see cref="ParentCategoryId"/> (e.g. "Mens > Shirts >
/// T-Shirts"). When publishing products to Polar, the category name is typically written into
/// the Polar product's <c>metadata</c> as a marker (e.g. <c>polar_sharp_category</c>).
/// </remarks>
public abstract record PolarCategoryBase : IPolarEntity, IPolarTimestamped
{
    /// <summary>Gets the category identifier (host-assigned, typically a GUID).</summary>
    public required string Id { get; init; }

    /// <summary>Gets the category's display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the category description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the parent category identifier (for hierarchical organization; null = top-level within a department).</summary>
    public string? ParentCategoryId { get; init; }

    /// <summary>Gets the owning department identifier (categories belong to one department).</summary>
    public string? DepartmentId { get; init; }

    /// <summary>Gets the display sort order within the parent / department (lower = earlier).</summary>
    public int SortOrder { get; init; }

    /// <summary>Gets the UTC timestamp the category was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
