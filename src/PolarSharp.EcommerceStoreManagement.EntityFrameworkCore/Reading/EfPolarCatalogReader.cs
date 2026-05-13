using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Reading;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Reading;

/// <summary>
/// Default <see cref="IPolarCatalogReader"/> implementation. Reads catalog entities from
/// <see cref="PolarCatalogDbContext"/> and merges per-field translations from
/// <see cref="IPolarCatalogTranslationCache"/> (with repository fallback on cache miss).
/// </summary>
/// <remarks>
/// <para>
/// Reassembly is field-by-field: for every translatable field on an entity, the reader looks
/// up <c>(language, fieldName)</c> in the cached <see cref="EntityTranslations"/>. When the
/// lookup hits, the translation replaces the master-language value; when it misses, the
/// master value passes through unchanged.
/// </para>
/// <para>
/// Cache is consulted first; on miss, the repository populates the cache before the read
/// returns. Subsequent reads — even for different languages on the same entity — hit cache.
/// </para>
/// </remarks>
internal sealed class EfPolarCatalogReader(
    PolarCatalogDbContext db,
    ITranslationRepository translations,
    IPolarCatalogTranslationCache cache,
    ILogger<EfPolarCatalogReader> logger) : IPolarCatalogReader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly PolarCatalogDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly ITranslationRepository _translations = translations ?? throw new ArgumentNullException(nameof(translations));
    private readonly IPolarCatalogTranslationCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ILogger<EfPolarCatalogReader> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<LocalProduct?> GetProductLocalizedAsync(ProductId productId, string language, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);

        var entity = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId.Value, ct)
            .ConfigureAwait(false);
        if (entity is null) return null;

        var variants = entity.HasVariants
            ? await _db.Variants.AsNoTracking()
                .Where(v => v.ProductId == entity.Id)
                .ToListAsync(ct).ConfigureAwait(false)
            : [];

        var categoryIds = await _db.ProductCategories.AsNoTracking()
            .Where(a => a.ProductId == entity.Id)
            .Select(a => a.CategoryId)
            .ToListAsync(ct).ConfigureAwait(false);

        var translations = await GetTranslationsAsync(entity.TenantId, CatalogTranslationEntityType.Product, entity.Id, ct).ConfigureAwait(false);

        return Project(entity, variants, categoryIds, translations, language);
    }

    public async Task<LocalProductVariant?> GetVariantLocalizedAsync(VariantId variantId, string language, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);

        var entity = await _db.Variants.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == variantId.Value, ct)
            .ConfigureAwait(false);
        if (entity is null) return null;

        var translations = await GetTranslationsAsync(entity.TenantId, CatalogTranslationEntityType.Variant, entity.Id, ct).ConfigureAwait(false);
        return Project(entity, translations, language);
    }

    public async Task<LocalCategory?> GetCategoryLocalizedAsync(CategoryId categoryId, string language, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);

        var entity = await _db.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoryId.Value, ct)
            .ConfigureAwait(false);
        if (entity is null) return null;

        var translations = await GetTranslationsAsync(entity.TenantId, CatalogTranslationEntityType.Category, entity.Id, ct).ConfigureAwait(false);
        return Project(entity, translations, language);
    }

    public async Task<LocalDepartment?> GetDepartmentLocalizedAsync(DepartmentId departmentId, string language, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);

        var entity = await _db.Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == departmentId.Value, ct)
            .ConfigureAwait(false);
        if (entity is null) return null;

        var translations = await GetTranslationsAsync(entity.TenantId, CatalogTranslationEntityType.Department, entity.Id, ct).ConfigureAwait(false);
        return Project(entity, translations, language);
    }

    // ── Translation lookup ─────────────────────────────────────────────────────────

    private async Task<EntityTranslations> GetTranslationsAsync(
        string tenantId,
        CatalogTranslationEntityType entityType,
        Guid entityId,
        CancellationToken ct)
    {
        var cached = await _cache.TryGetAllForEntityAsync(tenantId, entityType, entityId, ct).ConfigureAwait(false);
        if (cached is not null) return cached;

        var rows = await _translations.GetAllForEntityAsync(entityType, entityId, ct).ConfigureAwait(false);
        var assembled = Assemble(rows);

        try
        {
            await _cache.SetAllForEntityAsync(tenantId, entityType, entityId, assembled, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Cache warm-up never blocks a read — log and continue.
            _logger.LogDebug(ex, "Catalog reader: translation cache warm-up failed for {EntityType} {EntityId} (non-fatal).", entityType, entityId);
        }

        return assembled;
    }

    private static EntityTranslations Assemble(IReadOnlyList<CatalogTranslationEntity> rows)
    {
        if (rows.Count == 0) return EntityTranslations.Empty;

        var byLanguage = new Dictionary<string, Dictionary<string, string>>();
        foreach (var row in rows)
        {
            if (!byLanguage.TryGetValue(row.Language, out var byField))
            {
                byField = new Dictionary<string, string>();
                byLanguage[row.Language] = byField;
            }
            byField[row.FieldName] = row.TranslatedValue;
        }

        var projected = byLanguage.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyDictionary<string, string>)kv.Value);
        return new EntityTranslations(projected);
    }

    private static string? Translate(EntityTranslations translations, string language, string fieldName, string? masterValue)
    {
        var translated = translations.Get(language, fieldName);
        return string.IsNullOrEmpty(translated) ? masterValue : translated;
    }

    // ── Projection: EF entity → domain record, with per-field translation merge ─────

    private static LocalProduct Project(
        LocalProductEntity entity,
        IReadOnlyList<LocalProductVariantEntity> variantEntities,
        IReadOnlyList<Guid> categoryIds,
        EntityTranslations translations,
        string language)
    {
        var price = JsonSerializer.Deserialize<LocalPrice>(entity.PriceJson, JsonOptions)
                    ?? new LocalPrice { Kind = PriceKind.Free, Currency = "USD" };
        var attachedBenefits = JsonSerializer.Deserialize<List<Guid>>(entity.AttachedBenefitsJson, JsonOptions) ?? [];

        var translatedName = Translate(translations, language, "name", entity.MasterName) ?? entity.MasterName;
        var translatedDescription = Translate(translations, language, "description", entity.MasterDescription);

        return new LocalProduct
        {
            Id = entity.Id.ToString(),
            TenantId = entity.TenantId,
            // Polar's wire-format OrganizationId is not stored on the local catalog entity (which is
            // tenant-scoped, not Polar-org-scoped). Hosts that need it should pull it from their
            // TenantBusinessProfile.
            OrganizationId = entity.TenantId,
            Name = translatedName,
            Description = translatedDescription,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt,
            IsRecurring = price.IsRecurring,
            MasterName = translatedName,
            MasterDescription = translatedDescription,
            MasterLanguage = entity.MasterLanguage,
            Kind = entity.Kind,
            CategoryIds = [.. categoryIds.Select(g => new CategoryId(g))],
            TierGroupId = entity.TierGroupId is { } tg ? new TierGroupId(tg) : null,
            HasVariants = entity.HasVariants,
            Variants = [.. variantEntities.Select(v => Project(v, translations, language))],
            Price = price,
            AttachedBenefits = [.. attachedBenefits.Select(g => new BenefitId(g))],
            MsrpAmount = entity.MsrpAmount,
            MsrpCurrency = entity.MsrpCurrency,
            Manufacturer = entity.Manufacturer,
            Isbn = entity.Isbn,
            PolarProductId = entity.PolarProductId,
            LastPublishedAt = entity.LastPublishedAt,
            Status = entity.Status,
            IsFakeData = entity.IsFakeData,
        };
    }

    private static LocalProductVariant Project(LocalProductVariantEntity entity, EntityTranslations translations, string language)
    {
        var axes = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.AxesJson, JsonOptions)
                   ?? new Dictionary<string, string>();

        // Variants commonly translate axis-value labels (e.g. "red" → "rojo"). Field name convention:
        // "axis:<axisName>" per stored value. Falls back to the master value when no translation.
        var translatedAxes = new Dictionary<string, string>(axes.Count);
        foreach (var (axisName, axisValue) in axes)
            translatedAxes[axisName] = Translate(translations, language, $"axis:{axisName}", axisValue) ?? axisValue;

        return new LocalProductVariant
        {
            Id = new VariantId(entity.Id),
            Axes = translatedAxes,
            SurchargeAmount = entity.SurchargeAmount,
            Sku = entity.Sku,
            PolarProductId = entity.PolarProductId,
            LastPublishedAt = entity.LastPublishedAt,
            IsActive = entity.IsActive,
            InventoryCount = entity.InventoryCount,
            InventoryLowThreshold = entity.InventoryLowThreshold,
            LastStockChangedAt = entity.LastStockChangedAt,
        };
    }

    private static LocalCategory Project(LocalCategoryEntity entity, EntityTranslations translations, string language)
    {
        var translatedName = Translate(translations, language, "name", entity.MasterName) ?? entity.MasterName;
        var translatedDescription = Translate(translations, language, "description", entity.Description);

        return new LocalCategory
        {
            Id = entity.Id.ToString(),
            TenantId = entity.TenantId,
            Name = translatedName,
            Description = translatedDescription,
            ParentCategoryId = entity.ParentCategoryId?.ToString(),
            DepartmentId = entity.DepartmentId?.ToString(),
            SortOrder = entity.SortOrder,
            CreatedAt = entity.CreatedAt,
            MasterName = translatedName,
            IsFakeData = entity.IsFakeData,
        };
    }

    private static LocalDepartment Project(LocalDepartmentEntity entity, EntityTranslations translations, string language)
    {
        var translatedName = Translate(translations, language, "name", entity.MasterName) ?? entity.MasterName;
        var translatedDescription = Translate(translations, language, "description", entity.Description);

        return new LocalDepartment
        {
            Id = entity.Id.ToString(),
            TenantId = entity.TenantId,
            Name = translatedName,
            Description = translatedDescription,
            SortOrder = entity.SortOrder,
            CreatedAt = entity.CreatedAt,
            MasterName = translatedName,
            IsFakeData = entity.IsFakeData,
        };
    }
}
