namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for an in-progress shopping cart (pre-checkout). Polar.sh has no native
/// cart concept — only <see cref="PolarCheckoutBase"/> (single-step) and
/// <see cref="PolarOrderBase"/> (post-purchase). The shopping cart is a host-side concept
/// that converts to a Polar checkout when the customer initiates checkout.
/// </summary>
/// <remarks>
/// <para>
/// All currency amounts are in cents. The cart's total is recomputed on every line-item
/// add/remove/quantity-change.
/// </para>
/// <para>
/// Carts may be guest carts (<see cref="CustomerId"/> is null, only <see cref="CustomerEmail"/>
/// is known) or authenticated carts (both populated).
/// </para>
/// </remarks>
public abstract record PolarShoppingCartBase : IPolarEntity, IPolarTimestamped
{
    /// <summary>Gets the cart identifier (host-assigned, typically a GUID).</summary>
    public required string Id { get; init; }

    /// <summary>Gets the customer's email entered when the cart was created.</summary>
    public required string CustomerEmail { get; init; }

    /// <summary>Gets the customer identifier (null for guest carts).</summary>
    public string? CustomerId { get; init; }

    /// <summary>Gets the line items in the cart.</summary>
    public required IReadOnlyList<PolarCartLineItemBase> Items { get; init; }

    /// <summary>Gets the ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>Gets the subtotal in cents (sum of line totals, before discounts and tax).</summary>
    public int Subtotal { get; init; }

    /// <summary>Gets the total discount applied in cents (null if no discount).</summary>
    public int? DiscountAmount { get; init; }

    /// <summary>Gets the total tax applied in cents (null if no tax computed yet).</summary>
    public int? TaxAmount { get; init; }

    /// <summary>Gets the grand total in cents (Subtotal − DiscountAmount + TaxAmount).</summary>
    public int Total { get; init; }

    /// <summary>Gets the discount code applied to the cart (null if none).</summary>
    public string? DiscountCode { get; init; }

    /// <summary>Gets the UTC timestamp the cart will expire / be auto-pruned (typical: 24-72 hours).</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets the UTC timestamp the cart was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp the cart was last modified (item added/removed/quantity changed).</summary>
    public DateTimeOffset? ModifiedAt { get; init; }
}
