namespace PolarSharp.EcommerceStoreManagement.Cloning;

/// <summary>Clones a <see cref="LocalCategory"/>. Duplicate-prevention: name auto-suffixed with <c>" (Copy)"</c> on collision against the same-tenant catalog.</summary>
/// <remarks>
/// Category-product assignments are NOT cloned. A cloned category starts empty — the
/// operator assigns products to it explicitly. (Cloning the assignments would silently
/// duplicate every product into both categories, almost always wrong.)
/// </remarks>
public interface ICategoryCloningService
{
    /// <summary>Clones the category.</summary>
    Task<Result<LocalCategory, CloningError>> CloneAsync(
        CategoryId source,
        CloneCategoryOverrides? overrides = null,
        CloneCategoryOptions? options = null,
        CancellationToken ct = default);
}

/// <summary>Field overrides for a category clone.</summary>
public sealed record CloneCategoryOverrides
{
    /// <summary>Override the new category's master name. When <see langword="null"/>, the source name is suffixed with <c>" (Copy)"</c>.</summary>
    public string? NewMasterName { get; init; }
    /// <summary>Override the new category's description.</summary>
    public string? NewDescription { get; init; }
    /// <summary>Re-parent the clone under a different category. <see langword="null"/> = keep source parent.</summary>
    public CategoryId? NewParentCategoryId { get; init; }
    /// <summary>Re-assign the clone to a different department. <see langword="null"/> = keep source department.</summary>
    public DepartmentId? NewDepartmentId { get; init; }
}

/// <summary>Cascade toggles for a category clone.</summary>
public sealed record CloneCategoryOptions
{
    /// <summary>When <see langword="true"/>, every translation row is duplicated. Default <see langword="true"/>.</summary>
    public bool IncludeTranslations { get; init; } = true;
}
