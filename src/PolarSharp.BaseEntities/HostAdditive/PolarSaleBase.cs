namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Sale campaign — a time-bounded promotional discount with campaign
/// metadata (banner image, campaign name, marketing description). Inherits from
/// <see cref="PolarDiscountBase"/> because under the hood a Sale IS a Polar discount with
/// <see cref="PolarDiscountBase.StartsAt"/> and <see cref="PolarDiscountBase.EndsAt"/>; the
/// Sale concept adds richer host-side semantics for marketing campaigns.
/// </summary>
/// <remarks>
/// <see cref="ComputeStatus"/> returns the current campaign state by comparing the
/// configured <see cref="PolarDiscountBase.StartsAt"/> / <see cref="PolarDiscountBase.EndsAt"/>
/// to a supplied "now" timestamp.
/// </remarks>
public abstract record PolarSaleBase : PolarDiscountBase
{
    /// <summary>Gets the campaign's display name (e.g. "Black Friday 2026", "Summer Sale").</summary>
    public required string CampaignName { get; init; }

    /// <summary>Gets the URL of a banner image displayed on the storefront during the campaign.</summary>
    public string? BannerImageUrl { get; init; }

    /// <summary>Gets the marketing description shown on the campaign's landing page.</summary>
    public string? CampaignDescription { get; init; }

    /// <summary>
    /// Computes the current campaign status from <see cref="PolarDiscountBase.StartsAt"/>,
    /// <see cref="PolarDiscountBase.EndsAt"/>, and the supplied current time.
    /// </summary>
    /// <param name="now">The current UTC time. Inject from a clock service for testability.</param>
    /// <returns>
    /// <see cref="PolarSaleStatus.Pending"/> when StartsAt is in the future;
    /// <see cref="PolarSaleStatus.Ended"/> when EndsAt is in the past;
    /// <see cref="PolarSaleStatus.Active"/> otherwise.
    /// </returns>
    public PolarSaleStatus ComputeStatus(DateTimeOffset now) =>
        StartsAt is { } start && start > now ? PolarSaleStatus.Pending
        : EndsAt is { } end && end < now    ? PolarSaleStatus.Ended
                                             : PolarSaleStatus.Active;
}
