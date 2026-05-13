namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh checkout session. Property shape matches the
/// <c>checkout.created</c> / <c>checkout.updated</c> / <c>checkout.confirmed</c> /
/// <c>checkout.expired</c> webhook payloads exactly.
/// </summary>
/// <remarks>
/// Polar checkouts represent an in-progress purchase flow. On <see cref="Status"/> = Confirmed,
/// an order is created and <see cref="OrderId"/> is populated.
/// </remarks>
public abstract record PolarCheckoutBase : IPolarEntity, IPolarTimestamped, IPolarOrganizationScoped
{
    /// <summary>Gets the Polar.sh checkout identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the checkout's lifecycle status.</summary>
    public required PolarCheckoutStatus Status { get; init; }

    /// <summary>Gets the checkout total in cents.</summary>
    public required int Amount { get; init; }

    /// <summary>Gets the ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>Gets the customer identifier (when the customer is known; null for guest checkouts).</summary>
    public string? CustomerId { get; init; }

    /// <summary>Gets the customer's email entered at checkout (always populated, even for guest checkouts).</summary>
    public string? CustomerEmail { get; init; }

    /// <summary>Gets the UTC timestamp the checkout will expire if not confirmed.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets the order created on confirmation (null until <see cref="Status"/> is Confirmed).</summary>
    public string? OrderId { get; init; }

    /// <summary>Gets the Polar organization the checkout belongs to.</summary>
    public required string OrganizationId { get; init; }

    /// <summary>Gets the UTC timestamp the checkout was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
