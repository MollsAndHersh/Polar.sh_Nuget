namespace PolarSharp.EcommerceStorefronts.Abstractions.Checkout;

/// <summary>
/// Discrete state a <see cref="CheckoutSession"/> moves through as the order pipeline
/// runs. Values are stable wire identifiers safe to persist + surface in receipts.
/// </summary>
/// <remarks>
/// Values come in pairs for the pipeline body: each long-running stage has an
/// in-progress value (e.g. <see cref="ValidatingLineItems"/>) emitted on the
/// <c>CheckoutStageStarted</c> event and a completed value
/// (e.g. <see cref="LineItemsValidated"/>) emitted on the matching
/// <c>CheckoutStageCompleted</c> event. The terminal values are
/// <see cref="Completed"/> + <see cref="Failed"/>.
/// </remarks>
public enum CheckoutStatus
{
    /// <summary>The session was created but the pipeline has not started.</summary>
    Initiated,

    /// <summary>Server-side line-item revalidation is running.</summary>
    ValidatingLineItems,

    /// <summary>Line-item revalidation completed; prices + identifiers are trusted.</summary>
    LineItemsValidated,

    /// <summary>Inventory availability is being verified for each line.</summary>
    CheckingInventory,

    /// <summary>Inventory has been reserved (or confirmed sufficient) for every line.</summary>
    InventoryReserved,

    /// <summary>Cart + line discounts are being applied.</summary>
    ApplyingDiscounts,

    /// <summary>Discounts have been applied; the discount column on the order is final.</summary>
    DiscountsApplied,

    /// <summary>The tax provider is being asked for a sales-tax quote.</summary>
    QuotingTax,

    /// <summary>Tax has been quoted; the tax column on the order is final.</summary>
    TaxQuoted,

    /// <summary>The shipping provider is being asked for shipping rates.</summary>
    QuotingShipping,

    /// <summary>Shipping has been quoted (or skipped for digital orders).</summary>
    ShippingQuoted,

    /// <summary>The payment provider is capturing funds.</summary>
    CapturingPayment,

    /// <summary>Payment capture succeeded; funds are committed.</summary>
    PaymentCaptured,

    /// <summary>Fulfillment artefacts (labels, digital deliveries, benefit grants) are being created.</summary>
    Fulfilling,

    /// <summary>Fulfillment artefacts have been generated.</summary>
    Fulfilled,

    /// <summary>Customer / merchant notifications are being dispatched.</summary>
    Notifying,

    /// <summary>Notifications have been dispatched.</summary>
    Notified,

    /// <summary>The checkout finished successfully.</summary>
    Completed,

    /// <summary>The checkout failed; inspect the terminal pipeline event for details.</summary>
    Failed,
}
