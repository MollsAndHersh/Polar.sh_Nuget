namespace PolarSharp.EcommerceStorefronts.Abstractions.Cart;

/// <summary>Command to update the quantity of one line in the current cart.</summary>
/// <remarks>
/// Setting <see cref="Quantity"/> to zero is equivalent to removing the line; the
/// cart service is free to treat it as such for caller convenience.
/// </remarks>
public sealed record UpdateQuantityCommand
{
    /// <summary>The <see cref="CartLineItem.LineId"/> to update.</summary>
    public required string LineId { get; init; }

    /// <summary>The desired quantity; must be zero or positive.</summary>
    public required int Quantity { get; init; }

    /// <summary>Optional idempotency token; see <see cref="AddToCartCommand.IdempotencyToken"/>.</summary>
    public string? IdempotencyToken { get; init; }
}
