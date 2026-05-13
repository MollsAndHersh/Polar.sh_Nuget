using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement;

/// <summary>
/// A ladder of tiered products with cumulative benefits (e.g. <c>Basic</c> → <c>Advanced</c>
/// → <c>Ultimate</c>). Maps to N separate Polar Products at publish time — Polar has no
/// native "tier group" concept.
/// </summary>
/// <remarks>
/// <strong>Benefit inheritance.</strong> Each higher tier inherits the benefits of every
/// lower tier at publish. Hosts only need to attach the NEW benefits at each level — the
/// publisher computes the cumulative set. Each published product is tagged with
/// <c>polar_sharp_tier_group_id</c> + <c>polar_sharp_tier_rank</c> metadata so the host can
/// resolve "which tier is this Polar product?" later.
/// </remarks>
public sealed record LocalTierGroup : ITenantOwned, IFakeDataAware
{
    /// <summary>The tier-group identifier.</summary>
    public required TierGroupId Id { get; init; }

    /// <inheritdoc/>
    public required string TenantId { get; init; }

    /// <inheritdoc/>
    public bool IsFakeData { get; init; }

    /// <summary>Display name for the ladder (e.g. <c>"Pro Subscription Tiers"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The tier levels, ordered lowest-rank first.</summary>
    public required IReadOnlyList<TierLevel> Levels { get; init; }

    /// <summary>UTC creation timestamp.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>One rung in a tier ladder. The benefits listed here are layered on top of all lower-rank tiers' benefits at publish time.</summary>
public sealed record TierLevel
{
    /// <summary>Display name for this tier (e.g. <c>"Advanced"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>0-based rank within the ladder. Lower = cheaper / smaller bundle.</summary>
    public required int Rank { get; init; }

    /// <summary>The local product that represents this tier.</summary>
    public required ProductId ProductId { get; init; }

    /// <summary>Benefits unique to this tier — added on top of all lower-rank tiers' benefits.</summary>
    public IReadOnlyList<BenefitId> AddedBenefits { get; init; } = [];
}
