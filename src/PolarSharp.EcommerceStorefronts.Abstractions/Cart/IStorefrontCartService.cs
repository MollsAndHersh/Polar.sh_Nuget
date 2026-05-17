namespace PolarSharp.EcommerceStorefronts.Abstractions.Cart;

/// <summary>
/// Server-side cart operations. The storefront never trusts client-supplied prices
/// or line totals; every mutation funnels through this service so totals are
/// authoritative.
/// </summary>
/// <remarks>
/// The current cart is resolved from
/// <see cref="Identity.IStorefrontIdentityProvider"/> (for authenticated customers) or
/// from a guest session (see <c>PolarSharp.EcommerceStorefronts.GuestSessions</c>).
/// </remarks>
public interface IStorefrontCartService
{
    /// <summary>Returns the current cart, creating an empty one if none exists.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current <see cref="Cart"/>.</returns>
    Task<StorefrontResult<Cart>> GetCurrentCartAsync(CancellationToken ct);

    /// <summary>Adds a SKU to the current cart.</summary>
    /// <param name="cmd">The add-to-cart command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated cart.</returns>
    Task<StorefrontResult<Cart>> AddToCartAsync(AddToCartCommand cmd, CancellationToken ct);

    /// <summary>Updates the quantity of one line in the current cart.</summary>
    /// <param name="cmd">The quantity-update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated cart.</returns>
    Task<StorefrontResult<Cart>> UpdateLineQuantityAsync(UpdateQuantityCommand cmd, CancellationToken ct);

    /// <summary>Removes one line from the current cart.</summary>
    /// <param name="lineId">The line identifier to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated cart.</returns>
    Task<StorefrontResult<Cart>> RemoveLineAsync(string lineId, CancellationToken ct);

    /// <summary>Applies a discount code to the current cart.</summary>
    /// <param name="code">The discount code as entered by the customer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated cart.</returns>
    Task<StorefrontResult<Cart>> ApplyDiscountCodeAsync(string code, CancellationToken ct);

    /// <summary>Removes any active discount from the current cart.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated cart.</returns>
    Task<StorefrontResult<Cart>> RemoveDiscountAsync(CancellationToken ct);

    /// <summary>Attaches a shipping address to the current cart for tax + shipping quotation.</summary>
    /// <param name="address">The address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated cart.</returns>
    Task<StorefrontResult<Cart>> SetShippingAddressAsync(ShippingAddress address, CancellationToken ct);
}
