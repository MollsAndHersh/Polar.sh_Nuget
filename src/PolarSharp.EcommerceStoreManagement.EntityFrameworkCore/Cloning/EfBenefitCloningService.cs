using Microsoft.EntityFrameworkCore;
using PolarSharp;
using PolarSharp.EcommerceStoreManagement.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;

/// <summary>EF Core impl of <see cref="IBenefitCloningService"/>.</summary>
internal sealed class EfBenefitCloningService : IBenefitCloningService
{
    private readonly PolarCatalogDbContext _db;
    private readonly TimeProvider _time;

    public EfBenefitCloningService(PolarCatalogDbContext db, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(time);
        _db = db;
        _time = time;
    }

    public async Task<Result<LocalBenefit, CloningError>> CloneAsync(
        BenefitId source,
        CloneBenefitOverrides? overrides = null,
        CloneBenefitOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new CloneBenefitOptions();

        var src = await _db.Benefits.FirstOrDefaultAsync(b => b.Id == source.Value, ct).ConfigureAwait(false);
        if (src is null)
        {
            return Result<LocalBenefit, CloningError>.Failure(new CloningError(
                CloningErrorKind.SourceNotFound,
                $"Benefit '{source.Value}' was not found."));
        }

        string newName;
        if (!string.IsNullOrWhiteSpace(overrides?.NewName))
        {
            newName = overrides.NewName;
        }
        else
        {
            var picked = await CopySuffix.NextAvailableAsync(
                src.Name,
                (candidate, c) => _db.Benefits.AnyAsync(b => b.Name == candidate, c),
                ct).ConfigureAwait(false);
            if (picked is null)
            {
                return Result<LocalBenefit, CloningError>.Failure(new CloningError(
                    CloningErrorKind.NameCollisionExhausted,
                    $"Could not generate a non-colliding copy name after {CopySuffix.MaxAttempts} attempts."));
            }
            newName = picked;
        }

        var newId = Guid.NewGuid();
        var now = _time.GetUtcNow();
        var clone = new LocalBenefitEntity
        {
            Id = newId,
            BenefitKind = src.BenefitKind,
            Name = newName,
            Description = overrides?.NewDescription ?? src.Description,
            PropertiesJson = src.PropertiesJson,   // subtype-specific config copies verbatim
            PolarBenefitId = null,
            LastPublishedAt = null,
            Status = PublishStatus.Draft,
            CreatedAt = now,
            IsFakeData = src.IsFakeData,
        };
        _db.Benefits.Add(clone);

        if (options.IncludeTranslations)
        {
            var translations = await _db.Translations
                .Where(t => t.EntityType == CatalogTranslationEntityType.Benefit && t.EntityId == src.Id)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var t in translations)
            {
                _db.Translations.Add(new CatalogTranslationEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = t.TenantId,
                    EntityType = CatalogTranslationEntityType.Benefit,
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
            return Result<LocalBenefit, CloningError>.Failure(new CloningError(
                CloningErrorKind.PersistenceFailed,
                $"Failed to persist benefit clone: {ex.GetBaseException().Message}"));
        }

        // The polymorphic projection back to the LocalBenefit hierarchy is the consumer's
        // responsibility (they know which subtype they cloned and can deserialize
        // PropertiesJson accordingly). We return a generic clone shape — the caller can
        // immediately re-fetch via IBenefitRepository.GetAsync if they need the typed subtype.
        return Result<LocalBenefit, CloningError>.Success(new GenericBenefitProjection(clone));
    }

    /// <summary>Minimal LocalBenefit projection used as the return value from cloning. Callers fetch the typed subtype via IBenefitRepository.</summary>
    private sealed record GenericBenefitProjection : LocalBenefit
    {
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public GenericBenefitProjection(LocalBenefitEntity entity)
        {
            BenefitId = new BenefitId(entity.Id);
            Id = entity.Id.ToString();
            TenantId = entity.TenantId;
            Name = entity.Name;
            Description = entity.Description;
            OrganizationId = entity.TenantId;
            CreatedAt = entity.CreatedAt;
            PolarBenefitId = entity.PolarBenefitId;
            LastPublishedAt = entity.LastPublishedAt;
            Status = entity.Status;
            IsFakeData = entity.IsFakeData;
            Type = Enum.TryParse<PolarSharp.BaseEntities.PolarBenefitType>(entity.BenefitKind, ignoreCase: true, out var t)
                ? t : PolarSharp.BaseEntities.PolarBenefitType.Custom;
        }
    }
}
