namespace PolarSharp.EcommerceStorefronts.Abstractions.Customers;

/// <summary>One line on a completed order, as shown on the customer-facing order detail page.</summary>
public sealed record OrderLineItem
{
    /// <summary>The catalog provider's product identifier.</summary>
    public required string ProductId { get; init; }

    /// <summary>Product display name snapshotted at order time.</summary>
    public required string ProductName { get; init; }

    /// <summary>Variant identifier; <see langword="null"/> for single-SKU products.</summary>
    public string? VariantId { get; init; }

    /// <summary>Quantity purchased.</summary>
    public required int Quantity { get; init; }

    /// <summary>Unit price in minor units.</summary>
    public required int UnitAmountCents { get; init; }

    /// <summary>Line total in minor units (unit × quantity, less discount).</summary>
    public required int LineTotalCents { get; init; }
}
