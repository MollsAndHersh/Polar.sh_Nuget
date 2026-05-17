namespace PolarSharp.EcommerceStorefronts.Abstractions.Checkout;

/// <summary>
/// Discrete state a <see cref="CheckoutSession"/> moves through as the order pipeline
/// runs. Values are stable wire identifiers safe to persist + surface in receipts.
/// </summary>
public enum CheckoutStatus
{
    /// <summary>The session was created but the pipeline has not started.</summary>
    Initiated,

    /// <summary>Server-side line-item revalidation is running.</summary>
    ValidatingLineItems,

    /// <summary>Inventory availability is being verified for each line.</summary>
    CheckingInventory,

    /// <summary>Cart + line discounts are being applied.</summary>
    ApplyingDiscounts,

    /// <summary>The tax provider is being asked for a sales-tax quote.</summary>
    QuotingTax,

    /// <summary>The shipping provider is being asked for shipping rates.</summary>
    QuotingShipping,

    /// <summary>The payment provider is capturing funds.</summary>
    CapturingPayment,

    /// <summary>Fulfillment artefacts (labels, digital deliveries, benefit grants) are being created.</summary>
    Fulfilling,

    /// <summary>Customer / merchant notifications are being dispatched.</summary>
    Notifying,

    /// <summary>The checkout finished successfully.</summary>
    Completed,

    /// <summary>The checkout failed; inspect the terminal pipeline event for details.</summary>
    Failed,
}
