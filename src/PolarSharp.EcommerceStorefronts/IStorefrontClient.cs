using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Catalog;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Customers;

namespace PolarSharp.EcommerceStorefronts;

/// <summary>
/// Aggregating facade for storefront work — cart, checkout, customer, and catalog
/// services exposed under a single ambient client.
/// </summary>
/// <remarks>
/// Hosts inject <c>IStorefrontClient</c> when they want a single dependency rather
/// than threading four service interfaces through every Razor component. Lift-safe:
/// every property type lives in <c>PolarSharp.EcommerceStorefronts.Abstractions</c>.
/// </remarks>
public interface IStorefrontClient
{
    /// <summary>Read-only access to the catalog (browsing).</summary>
    IStorefrontCatalogProvider Catalog { get; }

    /// <summary>Cart mutations.</summary>
    IStorefrontCartService Cart { get; }

    /// <summary>Checkout orchestration.</summary>
    IStorefrontCheckoutService Checkout { get; }

    /// <summary>Customer self-service.</summary>
    IStorefrontCustomerService Customer { get; }
}
