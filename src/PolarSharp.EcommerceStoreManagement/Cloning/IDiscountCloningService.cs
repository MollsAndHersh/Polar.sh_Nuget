namespace PolarSharp.EcommerceStoreManagement.Cloning;

/// <summary>
/// Clones a <see cref="LocalDiscount"/>. The <c>(tenant_id, code)</c> unique index forces
/// special handling — see remarks.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Coupon-code default: NULL.</strong> By default the clone's <c>Code</c> is set to
/// <see langword="null"/> (becomes an automatic discount). This is the safe default — a
/// coupon clone with the SAME code would violate the <c>(tenant_id, code)</c> unique index
/// at insert time, and silently reusing the source's code would let the same coupon URL
/// resolve to two different discount records.
/// </para>
/// <para>
/// To set an explicit new code, pass <see cref="CloneDiscountOverrides.NewCode"/>. The
/// service checks the supplied code against the existing rows and returns
/// <see cref="CloningErrorKind.OverrideConflictsWithExistingRow"/> when it's already in use.
/// </para>
/// <para>
/// Polar-side state (<c>PolarDiscountId</c>, <c>LastPublishedAt</c>, <c>Status</c>) is reset
/// to a fresh <c>Draft</c>. The applicable-product list and validity window are copied
/// unchanged.
/// </para>
/// </remarks>
public interface IDiscountCloningService
{
    /// <summary>Clones the discount.</summary>
    Task<Result<LocalDiscount, CloningError>> CloneAsync(
        DiscountId source,
        CloneDiscountOverrides? overrides = null,
        CloneDiscountOptions? options = null,
        CancellationToken ct = default);
}

/// <summary>Field overrides for a discount clone.</summary>
public sealed record CloneDiscountOverrides
{
    /// <summary>Override the new discount's display name. When <see langword="null"/>, the source name is suffixed with <c>" (Copy)"</c>.</summary>
    public string? NewMasterName { get; init; }
    /// <summary>Set an explicit coupon code on the clone. When <see langword="null"/> the clone has no code (automatic discount). Conflicts with existing codes return <see cref="CloningErrorKind.OverrideConflictsWithExistingRow"/>.</summary>
    public string? NewCode { get; init; }
    /// <summary>Override the validity start.</summary>
    public DateTimeOffset? NewStartsAt { get; init; }
    /// <summary>Override the validity end.</summary>
    public DateTimeOffset? NewEndsAt { get; init; }
    /// <summary>Override the max redemptions cap.</summary>
    public int? NewMaxRedemptions { get; init; }
}

/// <summary>Cascade toggles for a discount clone.</summary>
public sealed record CloneDiscountOptions
{
    /// <summary>When <see langword="true"/>, the <c>ApplicableProductIds</c> list is copied. Default <see langword="true"/>.</summary>
    public bool IncludeApplicableProducts { get; init; } = true;
}
