using PolarSharp.EcommerceStorefronts.Abstractions.Paging;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Customers;

/// <summary>
/// Customer self-service operations — profile, addresses, order history, wallet.
/// </summary>
/// <remarks>
/// Every method is implicitly scoped to the current customer as resolved by
/// <see cref="Identity.IStorefrontIdentityProvider"/>; the service refuses operations
/// for guest sessions with a <see cref="StorefrontAuthenticationError"/>.
/// </remarks>
public interface IStorefrontCustomerService
{
    /// <summary>Loads the current customer's profile.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The profile.</returns>
    Task<StorefrontResult<CustomerProfile>> GetProfileAsync(CancellationToken ct);

    /// <summary>Updates the current customer's profile.</summary>
    /// <param name="cmd">Profile fields to update; null fields are left untouched.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="StorefrontUnit"/> on success.</returns>
    Task<StorefrontResult<StorefrontUnit>> UpdateProfileAsync(
        UpdateProfileCommand cmd,
        CancellationToken ct);

    /// <summary>Lists the customer's orders, paged.</summary>
    /// <param name="query">Paging / filter parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of <see cref="OrderSummary"/> rows.</returns>
    Task<StorefrontResult<PagedResult<OrderSummary>>> ListOrdersAsync(
        ListOrdersQuery query,
        CancellationToken ct);

    /// <summary>Loads one order's full detail.</summary>
    /// <param name="orderId">The Polar order identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The order detail.</returns>
    Task<StorefrontResult<OrderDetail>> GetOrderAsync(string orderId, CancellationToken ct);

    /// <summary>Lists the customer's saved addresses.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All saved addresses.</returns>
    Task<StorefrontResult<IReadOnlyList<SavedAddress>>> ListAddressesAsync(CancellationToken ct);

    /// <summary>Inserts or updates a saved address.</summary>
    /// <param name="address">The address to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted address.</returns>
    Task<StorefrontResult<SavedAddress>> SaveAddressAsync(SavedAddress address, CancellationToken ct);

    /// <summary>
    /// Returns the customer's prepaid wallet balance. Resolves to a zero-balance
    /// record (rather than an error) when the customer has never funded a wallet.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current wallet balance.</returns>
    Task<StorefrontResult<WalletBalance>> GetWalletBalanceAsync(CancellationToken ct);
}
