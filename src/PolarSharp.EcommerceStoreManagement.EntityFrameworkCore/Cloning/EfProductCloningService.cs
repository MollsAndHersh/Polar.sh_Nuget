using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarSharp;
using PolarSharp.EcommerceStoreManagement.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;

/// <summary>EF Core impl of <see cref="IProductCloningService"/>.</summary>
internal sealed class EfProductCloningService : IProductCloningService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly PolarCatalogDbContext _db;
    private readonly TimeProvider _time;

    public EfProductCloningService(PolarCatalogDbContext db, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(time);
        _db = db;
        _time = time;
    }

    public async Task<Result<LocalProduct, CloningError>> CloneAsync(
        ProductId source,
        CloneProductOverrides? overrides = null,
        CloneProductOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new CloneProductOptions();

        var src = await _db.Products.FirstOrDefaultAsync(p => p.Id == source.Value, ct).ConfigureAwait(false);
        if (src is null) return SourceNotFound(source.Value);

        // ── Choose the new name, avoiding the (tenant_id, master_name) unique-index collision. ──
        string newName;
        if (!string.IsNullOrWhiteSpace(overrides?.NewMasterName))
        {
            // Caller supplied an explicit name — verify it doesn't collide.
            if (await _db.Products.AnyAsync(p => p.MasterName == overrides.NewMasterName, ct).ConfigureAwait(false))
            {
                return Result<LocalProduct, CloningError>.Failure(new CloningError(
                    CloningErrorKind.OverrideConflictsWithExistingRow,
                    $"A product named '{overrides.NewMasterName}' already exists in this tenant."));
            }
            newName = overrides.NewMasterName;
        }
        else
        {
            var picked = await CopySuffix.NextAvailableAsync(
                src.MasterName,
                (candidate, c) => _db.Products.AnyAsync(p => p.MasterName == candidate, c),
                ct).ConfigureAwait(false);
            if (picked is null)
            {
                return Result<LocalProduct, CloningError>.Failure(new CloningError(
                    CloningErrorKind.NameCollisionExhausted,
                    $"Could not generate a non-colliding copy name after {CopySuffix.MaxAttempts} attempts. Supply an explicit NewMasterName."));
            }
            newName = picked;
        }

        // ── Build the cloned product entity. ──
        var newId = Guid.NewGuid();
        var now = _time.GetUtcNow();
        var clone = new LocalProductEntity
        {
            Id = newId,
            // TenantId is stamped by TenantAwareDbContextBase.StampNewEntities on SaveChanges.
            MasterName = newName,
            MasterDescription = overrides?.NewMasterDescription ?? src.MasterDescription,
            MasterLanguage = src.MasterLanguage,
            Kind = src.Kind,
            TierGroupId = src.TierGroupId,
            HasVariants = options.IncludeVariants && src.HasVariants,
            PriceJson = overrides?.NewPrice is not null
                ? JsonSerializer.Serialize(overrides.NewPrice, JsonOptions)
                : src.PriceJson,
            AttachedBenefitsJson = options.IncludeAttachedBenefits ? src.AttachedBenefitsJson : "[]",
            MsrpAmount = src.MsrpAmount,
            MsrpCurrency = src.MsrpCurrency,
            Manufacturer = src.Manufacturer,
            Isbn = src.Isbn,
            // Polar-side state RESET — the clone is a fresh draft.
            PolarProductId = null,
            LastPublishedAt = null,
            Status = PublishStatus.Draft,
            CreatedAt = now,
            ModifiedAt = null,
            IsFakeData = src.IsFakeData,
        };
        _db.Products.Add(clone);

        // ── Variant cascade. Each variant gets a fresh id. ──
        if (options.IncludeVariants)
        {
            var variants = await _db.Variants
                .Where(v => v.ProductId == src.Id)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var v in variants)
            {
                _db.Variants.Add(new LocalProductVariantEntity
                {
                    Id = Guid.NewGuid(),
                    ProductId = newId,
                    AxesJson = v.AxesJson,
                    SurchargeAmount = v.SurchargeAmount,
                    Sku = v.Sku,
                    PolarProductId = null,           // reset — variant gets a fresh Polar mapping on next publish
                    LastPublishedAt = null,
                    IsActive = v.IsActive,
                    InventoryCount = v.InventoryCount,
                    InventoryLowThreshold = v.InventoryLowThreshold,
                    LastStockChangedAt = null,
                    IsFakeData = v.IsFakeData,
                });
            }
        }

        // ── Category-assignment cascade. Caller-supplied override replaces; default copies source. ──
        var categoryIdsToAssign = overrides?.NewCategoryIds switch
        {
            { } explicitList => explicitList.Select(c => c.Value).ToList(),
            null when options.IncludeCategoryAssignments => await _db.ProductCategories
                .Where(a => a.ProductId == src.Id)
                .Select(a => a.CategoryId)
                .ToListAsync(ct).ConfigureAwait(false),
            _ => [],
        };
        foreach (var catId in categoryIdsToAssign)
        {
            _db.ProductCategories.Add(new LocalProductCategoryAssignmentEntity
            {
                Id = Guid.NewGuid(),
                ProductId = newId,
                CategoryId = catId,
                AssignedAt = now,
                IsFakeData = src.IsFakeData,
            });
        }

        // ── Translation cascade. Every (language, field) row is duplicated under the new entity id. ──
        if (options.IncludeTranslations)
        {
            var translations = await _db.Translations
                .Where(t => t.EntityType == CatalogTranslationEntityType.Product && t.EntityId == src.Id)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var t in translations)
            {
                _db.Translations.Add(new CatalogTranslationEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = t.TenantId,
                    EntityType = CatalogTranslationEntityType.Product,
                    EntityId = newId,
                    Language = t.Language,
                    FieldName = t.FieldName,
                    TranslatedValue = t.TranslatedValue,
                    IsMachineTranslated = t.IsMachineTranslated,
                    SourceProvider = t.SourceProvider,
                    SourceModel = t.SourceModel,
                    IsFakeData = t.IsFakeData,
                    CreatedAt = now,
                });
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            return Result<LocalProduct, CloningError>.Failure(new CloningError(
                CloningErrorKind.PersistenceFailed,
                $"Failed to persist product clone: {ex.GetBaseException().Message}"));
        }

        // ── Project the cloned entity back to the public record shape. ──
        var attachedBenefits = JsonSerializer.Deserialize<List<Guid>>(clone.AttachedBenefitsJson, JsonOptions)
                               ?? [];
        var price = JsonSerializer.Deserialize<LocalPrice>(clone.PriceJson, JsonOptions)
                    ?? new LocalPrice { Kind = PriceKind.Free, Currency = "USD" };

        var product = new LocalProduct
        {
            Id = clone.Id.ToString(),
            TenantId = clone.TenantId,
            OrganizationId = src.TenantId,    // src.OrganizationId mirror would come from the business profile; fallback to tenant id for the projection
            Name = clone.MasterName,
            CreatedAt = clone.CreatedAt,
            MasterName = clone.MasterName,
            MasterDescription = clone.MasterDescription,
            MasterLanguage = clone.MasterLanguage,
            Kind = clone.Kind,
            CategoryIds = [.. categoryIdsToAssign.Select(g => new CategoryId(g))],
            TierGroupId = clone.TierGroupId is { } tg ? new TierGroupId(tg) : null,
            HasVariants = clone.HasVariants,
            Price = price,
            AttachedBenefits = [.. attachedBenefits.Select(g => new BenefitId(g))],
            MsrpAmount = clone.MsrpAmount,
            MsrpCurrency = clone.MsrpCurrency,
            Manufacturer = clone.Manufacturer,
            Isbn = clone.Isbn,
            Status = clone.Status,
            IsFakeData = clone.IsFakeData,
        };
        return Result<LocalProduct, CloningError>.Success(product);
    }

    private static Result<LocalProduct, CloningError> SourceNotFound(Guid sourceId) =>
        Result<LocalProduct, CloningError>.Failure(new CloningError(
            CloningErrorKind.SourceNotFound,
            $"Product '{sourceId}' was not found in the current tenant's catalog."));
}
