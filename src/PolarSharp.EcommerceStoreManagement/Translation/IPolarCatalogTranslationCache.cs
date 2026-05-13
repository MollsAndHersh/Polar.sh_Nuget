namespace PolarSharp.EcommerceStoreManagement.Translation;

/// <summary>
/// Cache for assembled per-entity translation sets — one cache entry per entity holds every
/// language × every field for that entity. Lets the reader return any language without a
/// round-trip to SQL after the first warm-up.
/// </summary>
/// <remarks>
/// <strong>Per-entity granularity</strong> — entities are small (≤10 fields × ≤10 languages
/// = ≤100 strings); per-entity is faster to evict, faster to warm. A single language switch
/// in the host UI triggers zero new cache fetches.
/// </remarks>
public interface IPolarCatalogTranslationCache
{
    /// <summary>Returns the assembled translation set for the entity, or <see langword="null"/> on miss.</summary>
    ValueTask<EntityTranslations?> TryGetAllForEntityAsync(
        string tenantId,
        CatalogTranslationEntityType entityType,
        Guid entityId,
        CancellationToken ct = default);

    /// <summary>Stores the full translation set for the entity.</summary>
    ValueTask SetAllForEntityAsync(
        string tenantId,
        CatalogTranslationEntityType entityType,
        Guid entityId,
        EntityTranslations translations,
        CancellationToken ct = default);

    /// <summary>Removes the cache entry for the entity. Called on every translation insert / update / delete.</summary>
    ValueTask InvalidateAsync(
        string tenantId,
        CatalogTranslationEntityType entityType,
        Guid entityId,
        CancellationToken ct = default);

    /// <summary>Bulk-invalidates multiple entity cache entries — used by data-seeding cleanup and other batch operations.</summary>
    ValueTask InvalidateManyAsync(
        string tenantId,
        IEnumerable<(CatalogTranslationEntityType EntityType, Guid EntityId)> entities,
        CancellationToken ct = default);
}

/// <summary>The discriminator for which catalog entity type a translation row belongs to.</summary>
public enum CatalogTranslationEntityType
{
    /// <summary>A <see cref="LocalProduct"/>.</summary>
    Product,
    /// <summary>A <see cref="LocalProduct"/> in service mode (same table; discriminator on the row).</summary>
    Service,
    /// <summary>A <see cref="LocalProductVariant"/>.</summary>
    Variant,
    /// <summary>A <see cref="LocalCategory"/>.</summary>
    Category,
    /// <summary>A <see cref="LocalDepartment"/>.</summary>
    Department,
    /// <summary>A sale campaign (extends <see cref="LocalDiscount"/>).</summary>
    Sale,
    /// <summary>A <see cref="LocalBenefit"/>.</summary>
    Benefit,
}

/// <summary>
/// The cached translation set for one entity: language code → field name → translated value.
/// </summary>
public sealed record EntityTranslations(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ByLanguageThenField)
{
    /// <summary>Returns the translated value for <paramref name="language"/> + <paramref name="fieldName"/>, or <see langword="null"/> when no translation exists.</summary>
    public string? Get(string language, string fieldName) =>
        ByLanguageThenField.TryGetValue(language, out var byField) && byField.TryGetValue(fieldName, out var value)
            ? value : null;

    /// <summary>An empty translation set — equivalent to a cache hit with no rows.</summary>
    public static EntityTranslations Empty { get; } = new(new Dictionary<string, IReadOnlyDictionary<string, string>>());
}
