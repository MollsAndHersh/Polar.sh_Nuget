namespace PolarSharp.EcommerceStorefronts.Abstractions.Tax;

/// <summary>The tax-provider's quote for a cart.</summary>
public sealed record TaxQuote
{
    /// <summary>Total tax for the order in minor units.</summary>
    public required int TotalTaxCents { get; init; }

    /// <summary>Per-line tax breakdown in minor units, keyed by <see cref="Cart.CartLineItem.LineId"/>.</summary>
    public required IReadOnlyDictionary<string, int> LineTaxCents { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Provider-specific quote identifier the pipeline echoes back to
    /// <see cref="IStorefrontTaxProvider.RecordTransactionAsync"/> after capture.
    /// </summary>
    public string? QuoteId { get; init; }
}
