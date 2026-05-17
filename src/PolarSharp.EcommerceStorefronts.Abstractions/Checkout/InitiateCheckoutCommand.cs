using PolarSharp.EcommerceStorefronts.Abstractions.Cart;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Checkout;

/// <summary>Command to convert the current cart into a <see cref="CheckoutSession"/>.</summary>
/// <remarks>
/// The cart is identified by the current identity + guest session; the command carries
/// only the customer-collected inputs the cart did not already have (billing email for
/// guests, marketing-opt-in, etc.).
/// </remarks>
public sealed record InitiateCheckoutCommand
{
    /// <summary>The customer's email; required for guest checkout, otherwise inferred from identity.</summary>
    public string? CustomerEmail { get; init; }

    /// <summary>Optional billing address; defaults to the cart's shipping address when not supplied.</summary>
    public ShippingAddress? BillingAddress { get; init; }

    /// <summary>True when the customer opted into marketing communications.</summary>
    public bool MarketingOptIn { get; init; }

    /// <summary>
    /// Optional idempotency token; identical token + cart short-circuits to the existing
    /// session. Surfaces as the <c>X-Storefront-Idempotency</c> header.
    /// </summary>
    public string? IdempotencyToken { get; init; }
}
