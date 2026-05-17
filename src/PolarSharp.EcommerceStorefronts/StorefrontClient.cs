using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Catalog;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Customers;

namespace PolarSharp.EcommerceStorefronts;

/// <summary>
/// Default <see cref="IStorefrontClient"/> implementation; a thin wrapper that exposes
/// the four storefront services as ambient properties.
/// </summary>
/// <remarks>
/// All work is delegated to the injected services. Hosts that need to swap behaviour
/// register a different implementation of the individual service rather than
/// subclassing this client.
/// </remarks>
public sealed class StorefrontClient : IStorefrontClient
{
    /// <summary>Constructs a new client over the supplied service instances.</summary>
    /// <param name="catalog">The catalog provider.</param>
    /// <param name="cart">The cart service.</param>
    /// <param name="checkout">The checkout service.</param>
    /// <param name="customer">The customer service.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any argument is <see langword="null"/>.
    /// </exception>
    public StorefrontClient(
        IStorefrontCatalogProvider catalog,
        IStorefrontCartService cart,
        IStorefrontCheckoutService checkout,
        IStorefrontCustomerService customer)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(cart);
        ArgumentNullException.ThrowIfNull(checkout);
        ArgumentNullException.ThrowIfNull(customer);
        Catalog = catalog;
        Cart = cart;
        Checkout = checkout;
        Customer = customer;
    }

    /// <inheritdoc/>
    public IStorefrontCatalogProvider Catalog { get; }

    /// <inheritdoc/>
    public IStorefrontCartService Cart { get; }

    /// <inheritdoc/>
    public IStorefrontCheckoutService Checkout { get; }

    /// <inheritdoc/>
    public IStorefrontCustomerService Customer { get; }
}
