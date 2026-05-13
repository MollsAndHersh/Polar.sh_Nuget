namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh order. Property shape matches the
/// <c>order.created</c> / <c>order.paid</c> / <c>order.refunded</c> / <c>order.updated</c>
/// webhook payload exactly — host applications that inherit from this base can directly
/// receive webhook payloads with zero translation.
/// </summary>
/// <remarks>
/// <para>
/// All currency amounts (<see cref="Amount"/>, <see cref="TaxAmount"/>,
/// <see cref="AppliedBalanceAmount"/>) are in <strong>cents</strong> (or the smallest
/// currency unit for non-decimal currencies).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed record MyShopOrder : PolarOrderBase
/// {
///     public string MyHostInternalReference { get; init; } = "";
///     public bool RequiresGiftWrapping { get; init; }
/// }
///
/// // In a webhook handler:
/// public Task HandleAsync(OrderCreatedEvent evt, CancellationToken ct)
/// {
///     PolarOrderBase order = evt.Data;          // implicit upcast — no mapping
///     // ...
/// }
/// </code>
/// </example>
public abstract record PolarOrderBase : IPolarEntity, IPolarTimestamped, IPolarMetadata, IPolarOrganizationScoped
{
    /// <summary>Gets the Polar.sh order identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the human-readable order number (e.g. "ORD-12345").</summary>
    public required string Number { get; init; }

    /// <summary>Gets the order's lifecycle status.</summary>
    public required PolarOrderStatus Status { get; init; }

    /// <summary>Gets the gross order amount in cents (before refunds).</summary>
    public required int Amount { get; init; }

    /// <summary>Gets the tax amount in cents.</summary>
    public int TaxAmount { get; init; }

    /// <summary>Gets the customer-balance amount applied in cents (e.g. credit balance, gift card).</summary>
    public int AppliedBalanceAmount { get; init; }

    /// <summary>Gets the ISO 4217 currency code (e.g. "USD", "EUR").</summary>
    public required string Currency { get; init; }

    /// <summary>Gets the customer's name at billing time (snapshotted).</summary>
    public string? BillingName { get; init; }

    /// <summary>Gets the customer's billing address at order time.</summary>
    public PolarAddressBase? BillingAddress { get; init; }

    /// <summary>Gets the reason this order was generated. One of "purchase", "subscription_create", "subscription_cycle", etc.</summary>
    public string? BillingReason { get; init; }

    /// <summary>Gets the channel the order originated from. One of "web", "api", "embed".</summary>
    public string? Channel { get; init; }

    /// <summary>Gets the originating checkout session identifier (when applicable).</summary>
    public string? CheckoutId { get; init; }

    /// <summary>Gets the customer identifier.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Gets the Polar organization the order belongs to.</summary>
    public required string OrganizationId { get; init; }

    /// <summary>Gets the subscription identifier (when this order is a subscription cycle billing).</summary>
    public string? SubscriptionId { get; init; }

    /// <summary>Gets the line items on the order.</summary>
    public IReadOnlyList<PolarOrderLineItemBase> Items { get; init; } = [];

    /// <summary>Gets the URL to the customer-downloadable invoice PDF (null when no invoice yet).</summary>
    public string? InvoiceUrl { get; init; }

    /// <summary>Gets the order's free-form metadata key-value pairs.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the UTC timestamp the order was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp the order was fulfilled (payment captured + benefits granted).</summary>
    public DateTimeOffset? FulfilledAt { get; init; }
}
