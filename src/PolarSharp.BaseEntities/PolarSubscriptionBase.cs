namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh subscription. Property shape matches the
/// <c>subscription.created</c> / <c>.active</c> / <c>.updated</c> / <c>.canceled</c> /
/// <c>.uncanceled</c> / <c>.past_due</c> / <c>.revoked</c> webhook payloads exactly.
/// </summary>
public abstract record PolarSubscriptionBase : IPolarEntity, IPolarTimestamped, IPolarMetadata, IPolarOrganizationScoped
{
    /// <summary>Gets the Polar.sh subscription identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the subscription's lifecycle status.</summary>
    public required PolarSubscriptionStatus Status { get; init; }

    /// <summary>Gets the subscriber's customer identifier.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Gets the product the subscription is for.</summary>
    public required string ProductId { get; init; }

    /// <summary>Gets the price the subscription is billed at.</summary>
    public string? PriceId { get; init; }

    /// <summary>Gets the Polar organization the subscription belongs to.</summary>
    public required string OrganizationId { get; init; }

    /// <summary>Gets the ISO 4217 currency code (e.g. "USD", "EUR").</summary>
    public required string Currency { get; init; }

    /// <summary>Gets the start of the current billing period.</summary>
    public DateTimeOffset? CurrentPeriodStart { get; init; }

    /// <summary>Gets the end of the current billing period (next renewal date).</summary>
    public DateTimeOffset? CurrentPeriodEnd { get; init; }

    /// <summary>Gets the UTC timestamp the subscription was canceled (null if active).</summary>
    public DateTimeOffset? CanceledAt { get; init; }

    /// <summary>Gets the UTC timestamp the subscription will fully terminate after cancellation (null while active).</summary>
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>Gets the UTC timestamp the trial period started (null if no trial was offered).</summary>
    public DateTimeOffset? TrialStartsAt { get; init; }

    /// <summary>Gets the UTC timestamp the trial period ended (null if no trial was offered).</summary>
    public DateTimeOffset? TrialEndsAt { get; init; }

    /// <summary>Gets the subscription's free-form metadata key-value pairs.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the UTC timestamp the subscription was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp the subscription was last modified.</summary>
    public DateTimeOffset? ModifiedAt { get; init; }
}
