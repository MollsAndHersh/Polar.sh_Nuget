namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh product (one-time or recurring). Every PolarSharp product
/// representation — and any host application's product type — should inherit from this base
/// to share Polar's wire format.
/// </summary>
/// <remarks>
/// <para>
/// Polar emits this shape via <c>/v1/products/{id}</c> and on every product-scoped webhook
/// (<c>product.created</c>, <c>product.updated</c>).
/// </para>
/// <para>
/// Polar has NO native concept of variants, categories, or inventory — those live in
/// host-additive bases (<see cref="PolarCategoryBase"/>, <see cref="PolarInventoryRecordBase"/>)
/// and PolarSharp.EcommerceStoreManagement's catalog model.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed record MyShopProduct : PolarProductBase
/// {
///     public string InternalSku { get; init; } = "";
///     public decimal CostBasis { get; init; }
/// }
/// </code>
/// </example>
public abstract record PolarProductBase : IPolarEntity, IPolarTimestamped, IPolarMetadata, IPolarOrganizationScoped
{
    /// <summary>Gets the Polar.sh product identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the product name (single-language; localized variants live in PolarSharp.EcommerceStoreManagement's translation table).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the product description (single-language).</summary>
    public string? Description { get; init; }

    /// <summary>Gets the Polar organization the product belongs to.</summary>
    public required string OrganizationId { get; init; }

    /// <summary>Gets a value indicating whether the product is archived (hidden from new checkouts; existing subscriptions continue).</summary>
    public bool IsArchived { get; init; }

    /// <summary>Gets a value indicating whether the product bills on a recurring cadence (true) or is a one-time purchase (false).</summary>
    public bool IsRecurring { get; init; }

    /// <summary>Gets the recurring billing cadence (only meaningful when <see cref="IsRecurring"/> is <see langword="true"/>).</summary>
    public PolarRecurringInterval RecurringInterval { get; init; } = PolarRecurringInterval.None;

    /// <summary>Gets the count multiplier for the recurring interval (e.g. <c>RecurringInterval=Monthly</c> + <c>RecurringIntervalCount=3</c> = quarterly billing).</summary>
    public int? RecurringIntervalCount { get; init; }

    /// <summary>Gets the time unit for the free-trial period (only meaningful when <see cref="IsRecurring"/> is <see langword="true"/>).</summary>
    public PolarTrialInterval? TrialInterval { get; init; }

    /// <summary>Gets the count multiplier for the trial interval (e.g. <c>TrialInterval=Days</c> + <c>TrialIntervalCount=14</c> = 14-day trial).</summary>
    public int? TrialIntervalCount { get; init; }

    /// <summary>Gets the product's free-form metadata key-value pairs.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the UTC timestamp the product was created in Polar.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp the product was last modified in Polar.</summary>
    public DateTimeOffset? ModifiedAt { get; init; }
}
