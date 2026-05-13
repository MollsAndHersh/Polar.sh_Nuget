using Microsoft.EntityFrameworkCore;
using PolarSharp;
using PolarSharp.EcommerceStoreManagement.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;

/// <summary>EF Core impl of <see cref="IDiscountCloningService"/>.</summary>
internal sealed class EfDiscountCloningService : IDiscountCloningService
{
    private readonly PolarCatalogDbContext _db;
    private readonly TimeProvider _time;

    public EfDiscountCloningService(PolarCatalogDbContext db, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(time);
        _db = db;
        _time = time;
    }

    public async Task<Result<LocalDiscount, CloningError>> CloneAsync(
        DiscountId source,
        CloneDiscountOverrides? overrides = null,
        CloneDiscountOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new CloneDiscountOptions();

        var src = await _db.Discounts.FirstOrDefaultAsync(d => d.Id == source.Value, ct).ConfigureAwait(false);
        if (src is null)
        {
            return Result<LocalDiscount, CloningError>.Failure(new CloningError(
                CloningErrorKind.SourceNotFound,
                $"Discount '{source.Value}' was not found."));
        }

        // ── Name handling (auto-suffix unless overridden). ──
        string newName;
        if (!string.IsNullOrWhiteSpace(overrides?.NewMasterName))
        {
            newName = overrides.NewMasterName;
        }
        else
        {
            var picked = await CopySuffix.NextAvailableAsync(
                src.MasterName,
                (candidate, c) => _db.Discounts.AnyAsync(d => d.MasterName == candidate, c),
                ct).ConfigureAwait(false);
            if (picked is null)
            {
                return Result<LocalDiscount, CloningError>.Failure(new CloningError(
                    CloningErrorKind.NameCollisionExhausted,
                    $"Could not generate a non-colliding copy name after {CopySuffix.MaxAttempts} attempts."));
            }
            newName = picked;
        }

        // ── Coupon code handling. Default = null (becomes automatic discount). ──
        //    If the caller supplied a code, verify it doesn't already exist.
        string? newCode = null;
        if (!string.IsNullOrWhiteSpace(overrides?.NewCode))
        {
            if (await _db.Discounts.AnyAsync(d => d.Code == overrides.NewCode, ct).ConfigureAwait(false))
            {
                return Result<LocalDiscount, CloningError>.Failure(new CloningError(
                    CloningErrorKind.OverrideConflictsWithExistingRow,
                    $"Coupon code '{overrides.NewCode}' is already in use in this tenant."));
            }
            newCode = overrides.NewCode;
        }
        // else: cloned discount is automatic (no code) — the (tenant_id, code) unique index
        // is filtered on `code IS NOT NULL` so leaving it null cannot collide.

        var newId = Guid.NewGuid();
        var now = _time.GetUtcNow();
        var clone = new LocalDiscountEntity
        {
            Id = newId,
            MasterName = newName,
            Name = newName,                     // wire-format Name follows MasterName by default
            Code = newCode,
            Kind = src.Kind,
            Type = src.Type,
            AmountOff = src.AmountOff,
            PercentageOff = src.PercentageOff,
            Currency = src.Currency,
            DurationWire = src.DurationWire,
            DurationKind = src.DurationKind,
            DurationInMonths = src.DurationInMonths,
            StartsAt = overrides?.NewStartsAt ?? src.StartsAt,
            EndsAt = overrides?.NewEndsAt ?? src.EndsAt,
            MaxRedemptions = overrides?.NewMaxRedemptions ?? src.MaxRedemptions,
            ApplicableProductIdsJson = options.IncludeApplicableProducts ? src.ApplicableProductIdsJson : "[]",
            PolarDiscountId = null,
            LastPublishedAt = null,
            Status = PublishStatus.Draft,
            CreatedAt = now,
            IsFakeData = src.IsFakeData,
        };
        _db.Discounts.Add(clone);

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            return Result<LocalDiscount, CloningError>.Failure(new CloningError(
                CloningErrorKind.PersistenceFailed,
                $"Failed to persist discount clone: {ex.GetBaseException().Message}"));
        }

        var discount = new LocalDiscount
        {
            Id = clone.Id.ToString(),
            DiscountId = new DiscountId(clone.Id),
            TenantId = clone.TenantId,
            Name = clone.Name,
            MasterName = clone.MasterName,
            Code = clone.Code,
            Kind = clone.Kind,
            Type = clone.Type,
            AmountOff = clone.AmountOff,
            PercentageOff = clone.PercentageOff,
            Currency = clone.Currency,
            DurationKind = clone.DurationKind,
            StartsAt = clone.StartsAt,
            EndsAt = clone.EndsAt,
            MaxRedemptions = clone.MaxRedemptions,
            OrganizationId = clone.TenantId,
            CreatedAt = clone.CreatedAt,
            Status = clone.Status,
            IsFakeData = clone.IsFakeData,
        };
        return Result<LocalDiscount, CloningError>.Success(discount);
    }
}
