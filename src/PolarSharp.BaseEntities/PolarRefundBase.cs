namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh refund. Property shape matches the
/// <c>refund.created</c> / <c>refund.updated</c> webhook payloads exactly.
/// </summary>
/// <remarks>
/// All currency amounts are in cents (or the smallest currency unit for non-decimal currencies).
/// </remarks>
public abstract record PolarRefundBase : IPolarEntity, IPolarTimestamped, IPolarMetadata, IPolarOrganizationScoped
{
    /// <summary>Gets the Polar.sh refund identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the refund's lifecycle status.</summary>
    public required PolarRefundStatus Status { get; init; }

    /// <summary>Gets the categorical reason the refund was issued.</summary>
    public required PolarRefundReason Reason { get; init; }

    /// <summary>Gets the refund amount in cents.</summary>
    public required int Amount { get; init; }

    /// <summary>Gets the tax portion of the refund in cents.</summary>
    public int TaxAmount { get; init; }

    /// <summary>Gets the ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>Gets the order being refunded.</summary>
    public required string OrderId { get; init; }

    /// <summary>Gets the customer receiving the refund.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Gets the subscription identifier (when refunding a subscription cycle billing).</summary>
    public string? SubscriptionId { get; init; }

    /// <summary>Gets the Polar organization the refund belongs to.</summary>
    public required string OrganizationId { get; init; }

    /// <summary>Gets a value indicating whether benefits granted on the original purchase should be revoked.</summary>
    public bool RevokeBenefits { get; init; }

    /// <summary>Gets the refund's free-form metadata key-value pairs.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the UTC timestamp the refund was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp the refund was last modified.</summary>
    public DateTimeOffset? ModifiedAt { get; init; }
}
