namespace PolarSharp.Webhooks.Localization;

/// <summary>
/// Defines all localization key constants used by PolarSharp.Webhooks.
/// </summary>
/// <remarks>
/// Every key in this class must be present in <c>PolarWebhookMessages.resx</c> and
/// <c>PolarWebhookMessages.es-MX.resx</c>. A unit test in <c>PolarSharp.Webhooks.Tests</c>
/// verifies completeness for all supported cultures.
/// <para>
/// Toast keys follow the naming convention
/// <c>Toast_{event_type_with_underscores}_{Title|MessageTemplate}</c>.
/// The event type string is normalized by replacing <c>.</c> and <c>-</c> with <c>_</c>.
/// </para>
/// </remarks>
internal static class PolarWebhookMessageKeys
{
    // ── Webhook error messages ────────────────────────────────────────────────

    /// <summary>Displayed when HMAC signature verification fails.</summary>
    public const string Webhook_SignatureInvalid = nameof(Webhook_SignatureInvalid);

    /// <summary>Displayed when the webhook timestamp is outside the tolerance window.</summary>
    public const string Webhook_TimestampExpired = nameof(Webhook_TimestampExpired);

    /// <summary>Displayed when an unknown webhook event type is received. {0} = type string.</summary>
    public const string Webhook_UnknownEventType = nameof(Webhook_UnknownEventType);

    // ── Toast titles ─────────────────────────────────────────────────────────

    /// <summary>Title for order.created toast.</summary>
    public const string Toast_order_created_Title             = nameof(Toast_order_created_Title);

    /// <summary>Title for order.paid toast.</summary>
    public const string Toast_order_paid_Title                = nameof(Toast_order_paid_Title);

    /// <summary>Title for order.refunded toast.</summary>
    public const string Toast_order_refunded_Title            = nameof(Toast_order_refunded_Title);

    /// <summary>Title for subscription.created toast.</summary>
    public const string Toast_subscription_created_Title      = nameof(Toast_subscription_created_Title);

    /// <summary>Title for subscription.active toast.</summary>
    public const string Toast_subscription_active_Title       = nameof(Toast_subscription_active_Title);

    /// <summary>Title for subscription.canceled toast.</summary>
    public const string Toast_subscription_canceled_Title     = nameof(Toast_subscription_canceled_Title);

    /// <summary>Title for subscription.revoked toast.</summary>
    public const string Toast_subscription_revoked_Title      = nameof(Toast_subscription_revoked_Title);

    /// <summary>Title for checkout.created toast.</summary>
    public const string Toast_checkout_created_Title          = nameof(Toast_checkout_created_Title);

    /// <summary>Title for checkout.updated toast.</summary>
    public const string Toast_checkout_updated_Title          = nameof(Toast_checkout_updated_Title);

    /// <summary>Title for customer.created toast.</summary>
    public const string Toast_customer_created_Title          = nameof(Toast_customer_created_Title);

    /// <summary>Title for customer.updated toast.</summary>
    public const string Toast_customer_updated_Title          = nameof(Toast_customer_updated_Title);

    /// <summary>Title for benefit_grant.created toast.</summary>
    public const string Toast_benefit_grant_created_Title     = nameof(Toast_benefit_grant_created_Title);

    /// <summary>Title for benefit_grant.revoked toast.</summary>
    public const string Toast_benefit_grant_revoked_Title     = nameof(Toast_benefit_grant_revoked_Title);

    /// <summary>Title for refund.created toast.</summary>
    public const string Toast_refund_created_Title            = nameof(Toast_refund_created_Title);

    /// <summary>Title for refund.updated toast.</summary>
    public const string Toast_refund_updated_Title            = nameof(Toast_refund_updated_Title);

    // ── Toast message templates ───────────────────────────────────────────────

    /// <summary>Message template for order.created toast.</summary>
    public const string Toast_order_created_MessageTemplate         = nameof(Toast_order_created_MessageTemplate);

    /// <summary>Message template for order.paid toast.</summary>
    public const string Toast_order_paid_MessageTemplate            = nameof(Toast_order_paid_MessageTemplate);

    /// <summary>Message template for order.refunded toast.</summary>
    public const string Toast_order_refunded_MessageTemplate        = nameof(Toast_order_refunded_MessageTemplate);

    /// <summary>Message template for subscription.created toast.</summary>
    public const string Toast_subscription_created_MessageTemplate  = nameof(Toast_subscription_created_MessageTemplate);

    /// <summary>Message template for subscription.active toast.</summary>
    public const string Toast_subscription_active_MessageTemplate   = nameof(Toast_subscription_active_MessageTemplate);

    /// <summary>Message template for subscription.canceled toast.</summary>
    public const string Toast_subscription_canceled_MessageTemplate = nameof(Toast_subscription_canceled_MessageTemplate);

    /// <summary>Message template for subscription.revoked toast.</summary>
    public const string Toast_subscription_revoked_MessageTemplate  = nameof(Toast_subscription_revoked_MessageTemplate);

    /// <summary>Message template for checkout.created toast.</summary>
    public const string Toast_checkout_created_MessageTemplate      = nameof(Toast_checkout_created_MessageTemplate);

    /// <summary>Message template for checkout.updated toast.</summary>
    public const string Toast_checkout_updated_MessageTemplate      = nameof(Toast_checkout_updated_MessageTemplate);

    /// <summary>Message template for customer.created toast.</summary>
    public const string Toast_customer_created_MessageTemplate      = nameof(Toast_customer_created_MessageTemplate);

    /// <summary>Message template for customer.updated toast.</summary>
    public const string Toast_customer_updated_MessageTemplate      = nameof(Toast_customer_updated_MessageTemplate);

    /// <summary>Message template for benefit_grant.created toast.</summary>
    public const string Toast_benefit_grant_created_MessageTemplate = nameof(Toast_benefit_grant_created_MessageTemplate);

    /// <summary>Message template for benefit_grant.revoked toast.</summary>
    public const string Toast_benefit_grant_revoked_MessageTemplate = nameof(Toast_benefit_grant_revoked_MessageTemplate);

    /// <summary>Message template for refund.created toast.</summary>
    public const string Toast_refund_created_MessageTemplate        = nameof(Toast_refund_created_MessageTemplate);

    /// <summary>Message template for refund.updated toast.</summary>
    public const string Toast_refund_updated_MessageTemplate        = nameof(Toast_refund_updated_MessageTemplate);
}
