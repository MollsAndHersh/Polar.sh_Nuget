namespace PolarSharp.EcommerceStorefronts.Abstractions.Tax;

/// <summary>
/// Sales-tax provider abstraction. Implementations bridge to TaxJar, Avalara, or a
/// host's own tax engine.
/// </summary>
/// <remarks>
/// Called by the <c>QuoteTax</c> pipeline stage to attach tax to a cart, and again
/// from the post-fulfillment notify stage to record the transaction for filing.
/// </remarks>
public interface IStorefrontTaxProvider
{
    /// <summary>Quotes sales tax for the supplied cart shape.</summary>
    /// <param name="request">The quote request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tax quote.</returns>
    Task<StorefrontResult<TaxQuote>> QuoteAsync(QuoteTaxRequest request, CancellationToken ct);

    /// <summary>Records a completed sale with the tax provider for filing.</summary>
    /// <param name="request">The transaction request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="StorefrontUnit"/> on success.</returns>
    Task<StorefrontResult<StorefrontUnit>> RecordTransactionAsync(
        RecordTransactionRequest request,
        CancellationToken ct);

    /// <summary>Records a refund with the tax provider so the original sale is reversed.</summary>
    /// <param name="request">The refund request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="StorefrontUnit"/> on success.</returns>
    Task<StorefrontResult<StorefrontUnit>> RecordRefundAsync(
        RecordRefundRequest request,
        CancellationToken ct);
}
