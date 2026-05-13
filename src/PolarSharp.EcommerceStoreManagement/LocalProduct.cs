using PolarSharp.BaseEntities;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement;

/// <summary>
/// A locally-authored product or service that will be published to Polar.sh. Inherits the
/// canonical Polar product shape from <see cref="PolarProductBase"/> so the host's queries
/// see exactly what Polar emits in webhooks plus the host-additive catalog fields.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Variants:</strong> when <see cref="HasVariants"/> is true, the publish workflow
/// expands the local product into N Polar Products — one per <see cref="Variants"/> entry.
/// Each variant publishes with the master product's <see cref="PolarProductBase.Name"/>
/// suffixed by its axis values (e.g. <c>"Premium T-Shirt — Red, M"</c>) and carries
/// <c>polar_sharp_parent_id</c> metadata pointing back to <see cref="PolarProductBase.Id"/>.
/// </para>
/// <para>
/// <strong>Tier groups:</strong> when <see cref="TierGroupId"/> is set, this product is one
/// rung in a Basic / Advanced / Ultimate ladder. The benefits attached to each tier are
/// cumulative — Advanced inherits Basic's benefits, Ultimate inherits both.
/// </para>
/// </remarks>
public sealed record LocalProduct : PolarProductBase, ITenantOwned, IFakeDataAware
{
    /// <inheritdoc/>
    public required string TenantId { get; init; }

    /// <inheritdoc/>
    public bool IsFakeData { get; init; }

    /// <summary>Master-language product name.</summary>
    public required string MasterName { get; init; }

    /// <summary>Master-language description.</summary>
    public string? MasterDescription { get; init; }

    /// <summary>The language master text is authored in (e.g. <c>"en-US"</c>). Translations into <see cref="TenantBusinessProfile.SupportedLanguages"/> live in <c>catalog_translations</c>.</summary>
    public required string MasterLanguage { get; init; }

    /// <summary>Whether this is a Product or a Service.</summary>
    public required ProductKind Kind { get; init; }

    /// <summary>
    /// Categories this product belongs to (many-to-many). A product can live in zero, one, or
    /// multiple categories simultaneously — e.g. a wireless earbuds listing under both
    /// <c>"Audio"</c> and <c>"Mobile Accessories"</c>.
    /// </summary>
    /// <remarks>
    /// Categories themselves are tenant-scoped — the host's admin UI lets each tenant define
    /// their own taxonomy (typically right after onboarding, in their first catalog-setup
    /// session). Polar.sh has no native category concept; categories live entirely in the
    /// local catalog and are not published to Polar.
    /// </remarks>
    public IReadOnlyList<CategoryId> CategoryIds { get; init; } = [];

    /// <summary>Optional membership in a tier ladder.</summary>
    public TierGroupId? TierGroupId { get; init; }

    /// <summary>True when this product splays into multiple Polar Products at publish time.</summary>
    public bool HasVariants { get; init; }

    /// <summary>Variants (axis combinations). Ignored when <see cref="HasVariants"/> is false.</summary>
    public IReadOnlyList<LocalProductVariant> Variants { get; init; } = [];

    /// <summary>The pricing definition.</summary>
    public required LocalPrice Price { get; init; }

    /// <summary>Benefit identifiers attached to this product. Resolved to Polar benefit ids at publish time.</summary>
    public IReadOnlyList<BenefitId> AttachedBenefits { get; init; } = [];

    /// <summary>Manufacturer's suggested retail price in minor units. Display-only; not pushed to Polar.</summary>
    public int? MsrpAmount { get; init; }

    /// <summary>Currency code for <see cref="MsrpAmount"/>.</summary>
    public string? MsrpCurrency { get; init; }

    /// <summary>Manufacturer name. Display-only.</summary>
    public string? Manufacturer { get; init; }

    /// <summary>ISBN for books. Display-only.</summary>
    public string? Isbn { get; init; }

    /// <summary>The Polar product id assigned at publish time. <see langword="null"/> until first publish.</summary>
    public string? PolarProductId { get; init; }

    /// <summary>UTC of the most-recent successful publish to Polar.</summary>
    public DateTimeOffset? LastPublishedAt { get; init; }

    /// <summary>Current publish status.</summary>
    public PublishStatus Status { get; init; } = PublishStatus.Draft;
}

/// <summary>
/// A single variant within a <see cref="LocalProduct"/>. Each variant publishes as a
/// distinct Polar Product, with its axis values appended to the parent's name.
/// </summary>
public sealed record LocalProductVariant
{
    /// <summary>The variant identifier.</summary>
    public required VariantId Id { get; init; }

    /// <summary>Axis name → value (e.g. <c>{"color":"red","size":"M"}</c>). Order is preserved by the host when displaying the variant name.</summary>
    public required IReadOnlyDictionary<string, string> Axes { get; init; }

    /// <summary>Optional surcharge in minor units applied on top of the parent's price.</summary>
    public int? SurchargeAmount { get; init; }

    /// <summary>SKU code for inventory tracking. Optional.</summary>
    public string? Sku { get; init; }

    /// <summary>The Polar product id assigned to this variant on publish.</summary>
    public string? PolarProductId { get; init; }

    /// <summary>UTC of the most-recent successful publish.</summary>
    public DateTimeOffset? LastPublishedAt { get; init; }

    /// <summary>When false, the variant publishes with <c>is_archived: true</c> in Polar (hidden from checkout).</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Current on-hand inventory count. <see langword="null"/> means inventory is not tracked for this variant.</summary>
    public int? InventoryCount { get; init; }

    /// <summary>Threshold below which the inventory sync emits a low-stock event.</summary>
    public int? InventoryLowThreshold { get; init; }

    /// <summary>Computed — true when the variant is tracked and out of stock.</summary>
    public bool IsOutOfStock => InventoryCount is { } count && count <= 0;

    /// <summary>Computed — true when the variant is tracked and at or below the low-stock threshold.</summary>
    public bool IsLowStock => InventoryCount is { } count
        && InventoryLowThreshold is { } threshold
        && count <= threshold;

    /// <summary>UTC of the most-recent on-hand count change.</summary>
    public DateTimeOffset? LastStockChangedAt { get; init; }
}
