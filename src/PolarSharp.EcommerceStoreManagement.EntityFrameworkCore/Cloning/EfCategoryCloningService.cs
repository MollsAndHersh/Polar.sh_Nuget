using Microsoft.EntityFrameworkCore;
using PolarSharp;
using PolarSharp.EcommerceStoreManagement.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;

/// <summary>EF Core impl of <see cref="ICategoryCloningService"/>.</summary>
internal sealed class EfCategoryCloningService : ICategoryCloningService
{
    private readonly PolarCatalogDbContext _db;
    private readonly TimeProvider _time;

    public EfCategoryCloningService(PolarCatalogDbContext db, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(time);
        _db = db;
        _time = time;
    }

    public async Task<Result<LocalCategory, CloningError>> CloneAsync(
        CategoryId source,
        CloneCategoryOverrides? overrides = null,
        CloneCategoryOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new CloneCategoryOptions();

        var src = await _db.Categories.FirstOrDefaultAsync(c => c.Id == source.Value, ct).ConfigureAwait(false);
        if (src is null)
        {
            return Result<LocalCategory, CloningError>.Failure(new CloningError(
                CloningErrorKind.SourceNotFound,
                $"Category '{source.Value}' was not found in the current tenant's catalog."));
        }

        // Category names CAN repeat under different parents — so we only collision-check
        // within the same parent scope.
        var parentScope = overrides?.NewParentCategoryId?.Value ?? src.ParentCategoryId;
        string newName;
        if (!string.IsNullOrWhiteSpace(overrides?.NewMasterName))
        {
            newName = overrides.NewMasterName;
        }
        else
        {
            var picked = await CopySuffix.NextAvailableAsync(
                src.MasterName,
                (candidate, c) => _db.Categories
                    .AnyAsync(x => x.MasterName == candidate && x.ParentCategoryId == parentScope, c),
                ct).ConfigureAwait(false);
            if (picked is null)
            {
                return Result<LocalCategory, CloningError>.Failure(new CloningError(
                    CloningErrorKind.NameCollisionExhausted,
                    $"Could not generate a non-colliding copy name after {CopySuffix.MaxAttempts} attempts."));
            }
            newName = picked;
        }

        var newId = Guid.NewGuid();
        var now = _time.GetUtcNow();
        var clone = new LocalCategoryEntity
        {
            Id = newId,
            MasterName = newName,
            Description = overrides?.NewDescription ?? src.Description,
            ParentCategoryId = parentScope,
            DepartmentId = overrides?.NewDepartmentId?.Value ?? src.DepartmentId,
            SortOrder = src.SortOrder,
            CreatedAt = now,
            IsFakeData = src.IsFakeData,
        };
        _db.Categories.Add(clone);

        if (options.IncludeTranslations)
        {
            var translations = await _db.Translations
                .Where(t => t.EntityType == CatalogTranslationEntityType.Category && t.EntityId == src.Id)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var t in translations)
            {
                _db.Translations.Add(new CatalogTranslationEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = t.TenantId,
                    EntityType = CatalogTranslationEntityType.Category,
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
            return Result<LocalCategory, CloningError>.Failure(new CloningError(
                CloningErrorKind.PersistenceFailed,
                $"Failed to persist category clone: {ex.GetBaseException().Message}"));
        }

        var category = new LocalCategory
        {
            Id = clone.Id.ToString(),
            TenantId = clone.TenantId,
            Name = clone.MasterName,
            CreatedAt = clone.CreatedAt,
            MasterName = clone.MasterName,
            Description = clone.Description,
            ParentCategoryId = clone.ParentCategoryId?.ToString(),
            DepartmentId = clone.DepartmentId?.ToString(),
            SortOrder = clone.SortOrder,
            IsFakeData = clone.IsFakeData,
        };
        return Result<LocalCategory, CloningError>.Success(category);
    }
}
