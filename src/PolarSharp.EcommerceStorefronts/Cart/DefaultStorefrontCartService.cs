using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;

namespace PolarSharp.EcommerceStorefronts.Cart;

/// <summary>
/// Skeleton implementation of <see cref="IStorefrontCartService"/>. Registered by
/// <c>AddPolarStorefronts()</c> as the default; replaced wholesale by Phase 25.x
/// when the EF Core-backed persistence lands.
/// </summary>
/// <remarks>
/// Every method throws <see cref="NotImplementedException"/>. The class exists so
/// the DI graph composes cleanly today — host code wiring storefront pages against
/// the interface compiles, and the failure surfaces immediately on first call.
/// </remarks>
public sealed class DefaultStorefrontCartService : IStorefrontCartService
{
    private const string NotImplementedMessage =
        "Storefront cart persistence is scheduled for Phase 25.x — see the storefront-core architecture section of the plan.";

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<Abstractions.Cart.Cart>> GetCurrentCartAsync(CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<Abstractions.Cart.Cart>> AddToCartAsync(AddToCartCommand cmd, CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<Abstractions.Cart.Cart>> UpdateLineQuantityAsync(UpdateQuantityCommand cmd, CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<Abstractions.Cart.Cart>> RemoveLineAsync(string lineId, CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<Abstractions.Cart.Cart>> ApplyDiscountCodeAsync(string code, CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<Abstractions.Cart.Cart>> RemoveDiscountAsync(CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">Always thrown; concrete impl ships in Phase 25.x.</exception>
    public Task<StorefrontResult<Abstractions.Cart.Cart>> SetShippingAddressAsync(ShippingAddress address, CancellationToken ct)
        => throw new NotImplementedException(NotImplementedMessage);
}
