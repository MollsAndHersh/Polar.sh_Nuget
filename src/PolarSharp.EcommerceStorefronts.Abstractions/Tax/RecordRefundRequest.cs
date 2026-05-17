namespace PolarSharp.EcommerceStorefronts.Abstractions.Tax;

/// <summary>Request to record a refund with the tax provider so the original sale is reversed.</summary>
public sealed record RecordRefundRequest
{
    /// <summary>The Polar order identifier.</summary>
    public required string OrderId { get; init; }

    /// <summary>The Polar refund identifier.</summary>
    public required string RefundId { get; init; }

    /// <summary>Refund amount in minor units.</summary>
    public required int RefundAmountCents { get; init; }

    /// <summary>Tax amount refunded in minor units.</summary>
    public required int RefundTaxCents { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>UTC timestamp the refund was processed.</summary>
    public required DateTimeOffset RefundedAt { get; init; }
}
