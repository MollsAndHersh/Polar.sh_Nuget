using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Customers;
using PolarSharp.EcommerceStorefronts.Abstractions.Paging;

namespace PolarSharp.EcommerceStorefronts.Customers;

/// <summary>
/// Skeleton implementation of <see cref="IStorefrontCustomerService"/>. Registered by
/// <c>AddPolarStorefronts()</c>; replaced wholesale by Phase 25.x.
/// </summary>
/// <remarks>
/// All members throw <see cref="NotImplementedException"/>. Exists so the DI graph
/// composes cleanly today.
/// </remarks>
public sealed class DefaultStorefrontCustomerService : IStorefrontCustomerService
{
    private const string NotImplementedMessage =
        "Storefront customer self-service is scheduled for Phase 25.x — see the storefront-core architecture section of the plan.";

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<CustomerProfile>> GetProfileAsync(CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<StorefrontUnit>> UpdateProfileAsync(
        UpdateProfileCommand cmd,
        CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<PagedResult<OrderSummary>>> ListOrdersAsync(
        ListOrdersQuery query,
        CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<OrderDetail>> GetOrderAsync(string orderId, CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<IReadOnlyList<SavedAddress>>> ListAddressesAsync(CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<SavedAddress>> SaveAddressAsync(SavedAddress address, CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<WalletBalance>> GetWalletBalanceAsync(CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);
}
