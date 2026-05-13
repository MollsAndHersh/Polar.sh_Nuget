using PolarSharp.BaseEntities;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement;

/// <summary>
/// A locally-authored discount or coupon code. Inherits <see cref="PolarDiscountBase"/> so
/// the host sees Polar's exact webhook wire shape on the inherited members.
/// </summary>
public sealed record LocalDiscount : PolarDiscountBase, ITenantOwned, IFakeDataAware
{
    /// <inheritdoc/>
    public required string TenantId { get; init; }

    /// <inheritdoc/>
    public bool IsFakeData { get; init; }

    /// <summary>Local discount identifier.</summary>
    public required DiscountId DiscountId { get; init; }

    /// <summary>Master-language display name (typically not shown to customers — Polar's <c>name</c> on the inherited base is the customer-facing copy).</summary>
    public required string MasterName { get; init; }

    /// <summary>Discriminator — percentage vs fixed-amount.</summary>
    public required DiscountKind Kind { get; init; }

    /// <summary>Strongly-typed projection of the inherited <see cref="PolarDiscountBase.Duration"/> wire string. Source of truth on writes; the base string is set from this on publish.</summary>
    public DiscountDuration? DurationKind { get; init; }

    /// <summary>The Polar discount id assigned on first publish.</summary>
    public string? PolarDiscountId { get; init; }

    /// <summary>UTC of the most-recent successful publish.</summary>
    public DateTimeOffset? LastPublishedAt { get; init; }

    /// <summary>Current publish status.</summary>
    public PublishStatus Status { get; init; } = PublishStatus.Draft;
}
