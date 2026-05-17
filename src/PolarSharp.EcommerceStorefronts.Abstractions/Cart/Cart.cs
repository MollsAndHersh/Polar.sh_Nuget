namespace PolarSharp.EcommerceStorefronts.Abstractions.Cart;

/// <summary>
/// Pre-checkout shopping cart. Owned server-side; the storefront UI is a read view onto it.
/// </summary>
/// <remarks>
/// The cart is owner-keyed by either an authenticated customer or a guest session id
/// (see <c>PolarSharp.EcommerceStorefronts.GuestSessions</c>). Storefront-core defines
/// the shape; persistence lands in Phase 25.x.
/// <para>
/// Distinct from <c>PolarSharp.BaseEntities.PolarShoppingCartBase</c>: that record is
/// the host-additive persistence base; this record is the lift-safe transport object
/// returned by <see cref="IStorefrontCartService"/>.
/// </para>
/// </remarks>
public sealed record Cart
{
    /// <summary>The cart's stable identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The customer's identifier when authenticated; <see langword="null"/> for guest carts.</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>
    /// The guest session identifier when the cart belongs to an anonymous customer;
    /// <see langword="null"/> for authenticated carts.
    /// </summary>
    public Guid? GuestSessionId { get; init; }

    /// <summary>The cart's tenant scope; <see langword="null"/> in single-tenant deployments.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Line items currently in the cart.</summary>
    public IReadOnlyList<CartLineItem> LineItems { get; init; } = [];

    /// <summary>Optional discount code applied to the cart.</summary>
    public string? DiscountCode { get; init; }

    /// <summary>Optional shipping address used by tax and shipping providers.</summary>
    public ShippingAddress? ShippingAddress { get; init; }

    /// <summary>Computed totals; recomputed server-side on every mutation.</summary>
    public required CartTotals Totals { get; init; }

    /// <summary>UTC timestamp the cart was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC timestamp of the last mutation; <see langword="null"/> on a brand-new cart.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>UTC timestamp the cart will be auto-pruned; <see langword="null"/> when persistent.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
