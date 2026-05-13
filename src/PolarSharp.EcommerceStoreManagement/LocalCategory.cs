using PolarSharp.BaseEntities;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement;

/// <summary>
/// A catalog category — a finer-grained grouping than a <see cref="LocalDepartment"/>.
/// Polar.sh has no native category concept; categories live entirely locally and influence
/// only the host's UI / filtering.
/// </summary>
public sealed record LocalCategory : PolarCategoryBase, ITenantOwned, IFakeDataAware
{
    /// <inheritdoc/>
    public required string TenantId { get; init; }

    /// <inheritdoc/>
    public bool IsFakeData { get; init; }

    /// <summary>Master-language category name.</summary>
    public required string MasterName { get; init; }
}

/// <summary>
/// A catalog department — the coarsest grouping above categories (e.g. <c>"Clothing"</c>,
/// <c>"Electronics"</c>). Polar.sh has no native department concept; departments live
/// entirely locally.
/// </summary>
public sealed record LocalDepartment : PolarDepartmentBase, ITenantOwned, IFakeDataAware
{
    /// <inheritdoc/>
    public required string TenantId { get; init; }

    /// <inheritdoc/>
    public bool IsFakeData { get; init; }

    /// <summary>Master-language department name.</summary>
    public required string MasterName { get; init; }
}
