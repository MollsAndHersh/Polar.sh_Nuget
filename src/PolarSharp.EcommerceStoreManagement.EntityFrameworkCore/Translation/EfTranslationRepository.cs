using Microsoft.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Translation;

/// <summary>
/// EF Core-backed <see cref="ITranslationRepository"/>. Operates over the
/// <c>catalog_translations</c> table via <see cref="PolarCatalogDbContext.Translations"/>.
/// </summary>
/// <remarks>
/// <para>
/// The tenant scope is enforced by the DbContext's global tenant query filter; this repo
/// does NOT explicitly filter by tenant. All reads and writes happen within the current
/// tenant's view of the table.
/// </para>
/// <para>
/// <see cref="UpsertAsync"/> uses change-tracking to update existing rows and insert new
/// ones; idempotency is guaranteed by the unique index on
/// <c>(tenant_id, entity_type, entity_id, language, field_name)</c>.
/// </para>
/// </remarks>
internal sealed class EfTranslationRepository(PolarCatalogDbContext db, TimeProvider time)
    : ITranslationRepository
{
    private readonly PolarCatalogDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));

    public Task<IReadOnlyList<CatalogTranslationEntity>> GetAllForEntityAsync(
        CatalogTranslationEntityType entityType,
        Guid entityId,
        CancellationToken ct = default) =>
        ListAsync(entityType, entityId, tracked: false, ct);

    public async Task UpsertAsync(IReadOnlyList<CatalogTranslationEntity> rows, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0) return;

        var now = _time.GetUtcNow();
        var allEntityKeys = rows.Select(r => (r.EntityType, r.EntityId)).Distinct().ToList();

        // Single query loads every existing row across all affected entities so we can decide
        // per-row whether to update or insert without N+1.
        var existing = await _db.Translations
            .Where(t => allEntityKeys.Select(k => k.EntityType).Contains(t.EntityType)
                     && allEntityKeys.Select(k => k.EntityId).Contains(t.EntityId))
            .ToListAsync(ct).ConfigureAwait(false);

        var existingIndex = existing.ToDictionary(
            t => (t.EntityType, t.EntityId, t.Language, t.FieldName),
            t => t);

        foreach (var row in rows)
        {
            ArgumentNullException.ThrowIfNull(row);
            var key = (row.EntityType, row.EntityId, row.Language, row.FieldName);

            if (existingIndex.TryGetValue(key, out var current))
            {
                current.TranslatedValue = row.TranslatedValue;
                current.IsMachineTranslated = row.IsMachineTranslated;
                current.SourceProvider = row.SourceProvider;
                current.SourceModel = row.SourceModel;
                current.IsFakeData = row.IsFakeData;
                current.UpdatedAt = now;
            }
            else
            {
                _db.Translations.Add(new CatalogTranslationEntity
                {
                    Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id,
                    // TenantId is stamped by TenantAwareDbContextBase.StampNewEntities on SaveChanges
                    // when left empty here.
                    TenantId = row.TenantId,
                    EntityType = row.EntityType,
                    EntityId = row.EntityId,
                    Language = row.Language,
                    FieldName = row.FieldName,
                    TranslatedValue = row.TranslatedValue,
                    IsMachineTranslated = row.IsMachineTranslated,
                    SourceProvider = row.SourceProvider,
                    SourceModel = row.SourceModel,
                    IsFakeData = row.IsFakeData,
                    CreatedAt = now,
                });
            }
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAllForEntityAsync(
        CatalogTranslationEntityType entityType,
        Guid entityId,
        CancellationToken ct = default)
    {
        var tracked = await ListAsync(entityType, entityId, tracked: true, ct).ConfigureAwait(false);
        if (tracked.Count == 0) return;
        _db.Translations.RemoveRange(tracked);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAllFakeDataAsync(CancellationToken ct = default)
    {
        var tracked = await _db.Translations.Where(t => t.IsFakeData).ToListAsync(ct).ConfigureAwait(false);
        if (tracked.Count == 0) return;
        _db.Translations.RemoveRange(tracked);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<CatalogTranslationEntity>> ListAsync(
        CatalogTranslationEntityType entityType,
        Guid entityId,
        bool tracked,
        CancellationToken ct)
    {
        var query = _db.Translations.Where(t => t.EntityType == entityType && t.EntityId == entityId);
        if (!tracked) query = query.AsNoTracking();
        return await query.ToListAsync(ct).ConfigureAwait(false);
    }
}
