using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarSharp;
using PolarSharp.EcommerceStoreManagement.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;

/// <summary>EF Core impl of <see cref="ICheckoutLinkCloningService"/>.</summary>
internal sealed class EfCheckoutLinkCloningService : ICheckoutLinkCloningService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly PolarCatalogDbContext _db;
    private readonly TimeProvider _time;

    public EfCheckoutLinkCloningService(PolarCatalogDbContext db, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(time);
        _db = db;
        _time = time;
    }

    public async Task<Result<LocalCheckoutLinkConfig, CloningError>> CloneAsync(
        CheckoutLinkId source,
        CloneCheckoutLinkOverrides? overrides = null,
        CloneCheckoutLinkOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new CloneCheckoutLinkOptions();

        var src = await _db.CheckoutLinks.FirstOrDefaultAsync(l => l.Id == source.Value, ct).ConfigureAwait(false);
        if (src is null)
        {
            return Result<LocalCheckoutLinkConfig, CloningError>.Failure(new CloningError(
                CloningErrorKind.SourceNotFound,
                $"Checkout link '{source.Value}' was not found."));
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
                (candidate, c) => _db.CheckoutLinks.AnyAsync(l => l.Name == candidate, c),
                ct).ConfigureAwait(false);
            if (picked is null)
            {
                return Result<LocalCheckoutLinkConfig, CloningError>.Failure(new CloningError(
                    CloningErrorKind.NameCollisionExhausted,
                    $"Could not generate a non-colliding copy name after {CopySuffix.MaxAttempts} attempts."));
            }
            newName = picked;
        }

        var newId = Guid.NewGuid();
        var now = _time.GetUtcNow();
        var clone = new LocalCheckoutLinkEntity
        {
            Id = newId,
            Name = newName,
            ProductIdsJson = overrides?.NewProductIds is not null
                ? JsonSerializer.Serialize(overrides.NewProductIds.Select(p => p.Value), JsonOptions)
                : src.ProductIdsJson,
            SuccessUrl = overrides?.NewSuccessUrl ?? src.SuccessUrl,
            CancelUrl = overrides?.NewCancelUrl ?? src.CancelUrl,
            ThemeColor = src.ThemeColor,
            LogoUrl = src.LogoUrl,
            CustomFieldsJson = options.IncludeCustomFields ? src.CustomFieldsJson : "[]",
            AllowDiscountCodes = src.AllowDiscountCodes,
            RequireBillingAddress = src.RequireBillingAddress,
            PolarCheckoutLinkId = null,
            Status = PublishStatus.Draft,
            CreatedAt = now,
            IsFakeData = src.IsFakeData,
        };
        _db.CheckoutLinks.Add(clone);

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            return Result<LocalCheckoutLinkConfig, CloningError>.Failure(new CloningError(
                CloningErrorKind.PersistenceFailed,
                $"Failed to persist checkout link clone: {ex.GetBaseException().Message}"));
        }

        var productIds = JsonSerializer.Deserialize<List<Guid>>(clone.ProductIdsJson, JsonOptions) ?? [];
        var link = new LocalCheckoutLinkConfig
        {
            Id = new CheckoutLinkId(clone.Id),
            TenantId = clone.TenantId,
            Name = clone.Name,
            ProductIds = [.. productIds.Select(g => new ProductId(g))],
            SuccessUrl = clone.SuccessUrl,
            CancelUrl = clone.CancelUrl,
            ThemeColor = clone.ThemeColor,
            LogoUrl = clone.LogoUrl,
            AllowDiscountCodes = clone.AllowDiscountCodes,
            RequireBillingAddress = clone.RequireBillingAddress,
            Status = clone.Status,
            IsFakeData = clone.IsFakeData,
        };
        return Result<LocalCheckoutLinkConfig, CloningError>.Success(link);
    }
}
