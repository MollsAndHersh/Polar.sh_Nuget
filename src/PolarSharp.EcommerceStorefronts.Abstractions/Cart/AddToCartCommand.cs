namespace PolarSharp.EcommerceStorefronts.Abstractions.Cart;

/// <summary>Command to add a SKU to the current cart at the given quantity.</summary>
/// <remarks>
/// The cart service resolves the current cart from
/// <c>IStorefrontIdentityProvider</c> + the guest session, validates the SKU against
/// the catalog provider, and recomputes line + cart totals.
/// </remarks>
public sealed record AddToCartCommand
{
    /// <summary>The catalog provider's product identifier.</summary>
    public required string ProductId { get; init; }

    /// <summary>The catalog provider's variant identifier; <see langword="null"/> for single-SKU products.</summary>
    public string? VariantId { get; init; }

    /// <summary>Quantity to add; must be positive.</summary>
    public required int Quantity { get; init; }

    /// <summary>
    /// Optional idempotency token; identical token + body short-circuits to the
    /// existing cart state. Surfaces as the <c>X-Storefront-Idempotency</c> header.
    /// </summary>
    public string? IdempotencyToken { get; init; }
}
