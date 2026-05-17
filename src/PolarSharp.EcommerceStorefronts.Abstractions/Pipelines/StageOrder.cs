namespace PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

/// <summary>
/// Stable <c>Order</c>-property constants for every default pipeline stage so custom
/// stages can position themselves between the defaults without depending on stage types.
/// </summary>
/// <remarks>
/// Default stages are spaced by 1000 to leave room for hosts to insert their own
/// stages at e.g. <c>StageOrder.ApplyDiscounts + 500</c> (runs after the discount
/// stage but before the tax stage). Constants for the three pipelines share the
/// same numeric range because each pipeline orchestrator only inspects stages
/// implementing its own interface — no cross-pipeline collisions are possible.
/// </remarks>
public static class StageOrder
{
    // ---------- Order processing ----------

    /// <summary>Order value for <c>ValidateLineItemsStage</c>.</summary>
    public const int ValidateLineItems = 1000;

    /// <summary>Order value for <c>CheckInventoryStage</c>.</summary>
    public const int CheckInventory = 2000;

    /// <summary>Order value for <c>ApplyDiscountsStage</c>.</summary>
    public const int ApplyDiscounts = 3000;

    /// <summary>Order value for <c>QuoteTaxStage</c>.</summary>
    public const int QuoteTax = 4000;

    /// <summary>Order value for <c>QuoteShippingStage</c>.</summary>
    public const int QuoteShipping = 5000;

    /// <summary>Order value for <c>CapturePaymentStage</c>.</summary>
    public const int CapturePayment = 6000;

    /// <summary>Order value for <c>FulfillStage</c>.</summary>
    public const int Fulfill = 7000;

    /// <summary>Order value for <c>NotifyStage</c>.</summary>
    public const int Notify = 8000;

    // ---------- Subscription billing ----------

    /// <summary>Order value for <c>ValidateSubscriptionStage</c>.</summary>
    public const int SubscriptionValidate = 1000;

    /// <summary>Order value for <c>CheckPaymentMethodStage</c>.</summary>
    public const int SubscriptionCheckPaymentMethod = 2000;

    /// <summary>Order value for <c>ApplyProrationStage</c>.</summary>
    public const int SubscriptionApplyProration = 3000;

    /// <summary>Order value for the subscription-billing <c>CapturePaymentStage</c>.</summary>
    public const int SubscriptionCapturePayment = 4000;

    /// <summary>Order value for the subscription-billing <c>NotifyStage</c>.</summary>
    public const int SubscriptionNotify = 5000;

    // ---------- Refund processing ----------

    /// <summary>Order value for <c>ValidateRefundEligibilityStage</c>.</summary>
    public const int RefundValidateEligibility = 1000;

    /// <summary>Order value for <c>ComputeRefundAmountStage</c>.</summary>
    public const int RefundComputeAmount = 2000;

    /// <summary>Order value for <c>ExecuteRefundStage</c>.</summary>
    public const int RefundExecute = 3000;

    /// <summary>Order value for the refund-processing <c>NotifyStage</c>.</summary>
    public const int RefundNotify = 4000;
}
