namespace PolarSharp.EcommerceStoreManagement.Translation;

/// <summary>
/// Persistence abstraction over <c>catalog_translations</c>. EF Core implementation lives in
/// <c>PolarSharp.EcommerceStoreManagement.EntityFrameworkCore</c>.
/// </summary>
public interface ITranslationRepository
{
    /// <summary>Returns all translation rows for the supplied entity (every language × every field).</summary>
    Task<IReadOnlyList<CatalogTranslationEntity>> GetAllForEntityAsync(
        CatalogTranslationEntityType entityType,
        Guid entityId,
        CancellationToken ct = default);

    /// <summary>Inserts or updates the supplied rows. Idempotent on the (tenant, entity_type, entity_id, language, field_name) tuple.</summary>
    Task UpsertAsync(IReadOnlyList<CatalogTranslationEntity> rows, CancellationToken ct = default);

    /// <summary>Deletes every translation row for the supplied entity. Used when the entity itself is deleted.</summary>
    Task DeleteAllForEntityAsync(
        CatalogTranslationEntityType entityType,
        Guid entityId,
        CancellationToken ct = default);

    /// <summary>Deletes every translation row where <see cref="CatalogTranslationEntity.IsFakeData"/> is <see langword="true"/>. Used by the data-seeding fake-data cleanup.</summary>
    Task DeleteAllFakeDataAsync(CancellationToken ct = default);
}
