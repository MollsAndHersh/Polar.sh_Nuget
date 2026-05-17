using PolarSharp.EcommerceStorefronts.Abstractions.Cart;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Tax;

/// <summary>Request shape for <see cref="IStorefrontTaxProvider.QuoteAsync"/>.</summary>
public sealed record QuoteTaxRequest
{
    /// <summary>The cart identifier (for provider-side correlation).</summary>
    public required Guid CartId { get; init; }

    /// <summary>The destination address used to determine jurisdiction.</summary>
    public required ShippingAddress DestinationAddress { get; init; }

    /// <summary>Cart line items the tax provider must quote.</summary>
    public required IReadOnlyList<CartLineItem> LineItems { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }
}
