namespace PolarSharp.EcommerceStorefronts.Abstractions.Tax;

/// <summary>Request to record a completed sale with the tax provider for filing.</summary>
public sealed record RecordTransactionRequest
{
    /// <summary>The Polar order identifier.</summary>
    public required string OrderId { get; init; }

    /// <summary>The quote id the order was sold under (when available).</summary>
    public string? QuoteId { get; init; }

    /// <summary>Total amount in minor units.</summary>
    public required int TotalCents { get; init; }

    /// <summary>Total tax collected in minor units.</summary>
    public required int TaxCents { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>UTC timestamp the sale completed.</summary>
    public required DateTimeOffset CompletedAt { get; init; }
}
