using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Reading;

/// <summary>
/// Reads catalog entities with translations reassembled. Master-language values come from
/// the entity row; per-field translations are merged in from
/// <see cref="CatalogTranslationEntity"/> via the cache and (on miss) the repository.
/// </summary>
public interface IPolarCatalogReader
{
    /// <summary>Returns the product with translatable fields rewritten in <paramref name="language"/>. Falls back per-field to the master value when a translation is missing.</summary>
    Task<LocalProduct?> GetProductLocalizedAsync(ProductId productId, string language, CancellationToken ct = default);

    /// <summary>Returns the variant with translatable fields rewritten in <paramref name="language"/>.</summary>
    Task<LocalProductVariant?> GetVariantLocalizedAsync(VariantId variantId, string language, CancellationToken ct = default);

    /// <summary>Returns the category with translatable fields rewritten in <paramref name="language"/>.</summary>
    Task<LocalCategory?> GetCategoryLocalizedAsync(CategoryId categoryId, string language, CancellationToken ct = default);

    /// <summary>Returns the department with translatable fields rewritten in <paramref name="language"/>.</summary>
    Task<LocalDepartment?> GetDepartmentLocalizedAsync(DepartmentId departmentId, string language, CancellationToken ct = default);
}
