using System.Text.Json.Serialization;

namespace PolarSharp.Webhooks.Events;

// ── Shared value types ─────────────────────────────────────────────────────

/// <summary>Customer context carried in Polar webhook event payloads.</summary>
public sealed record WebhookCustomer
{
    /// <summary>Gets the Polar customer ID.</summary>
    [JsonPropertyName("id")] public string? Id { get; init; }

    /// <summary>Gets the customer's email address.</summary>
    [JsonPropertyName("email")] public string? Email { get; init; }

    /// <summary>Gets the customer's display name.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }
}

/// <summary>Product context carried in Polar webhook event payloads.</summary>
public sealed record WebhookProduct
{
    /// <summary>Gets the Polar product ID.</summary>
    [JsonPropertyName("id")] public string? Id { get; init; }

    /// <summary>Gets the product display name.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }
}

/// <summary>Price tier context carried in Polar webhook event payloads.</summary>
public sealed record WebhookPrice
{
    /// <summary>Gets the price ID.</summary>
    [JsonPropertyName("id")] public string? Id { get; init; }

    /// <summary>Gets the price display name (e.g., <c>"Monthly"</c>, <c>"Annual"</c>).</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Gets the price amount in minor currency units (e.g., cents).</summary>
    [JsonPropertyName("price_amount")] public int? Amount { get; init; }

    /// <summary>Gets the ISO 4217 currency code.</summary>
    [JsonPropertyName("price_currency")] public string? Currency { get; init; }
}

/// <summary>Line item on a Polar order.</summary>
public sealed record WebhookOrderItem
{
    /// <summary>Gets the product ID for this line item.</summary>
    [JsonPropertyName("product_id")] public string? ProductId { get; init; }

    /// <summary>Gets the product name for this line item.</summary>
    [JsonPropertyName("product_name")] public string? ProductName { get; init; }

    /// <summary>Gets the unit price in minor currency units.</summary>
    [JsonPropertyName("price_amount")] public int? PriceAmount { get; init; }

    /// <summary>Gets the ISO 4217 currency code.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }
}

// ── Order ──────────────────────────────────────────────────────────────────

/// <summary>Payload data shared across all <c>order.*</c> webhook events.</summary>
public sealed record WebhookOrderData
{
    /// <summary>Gets the Polar order ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Gets the order status at event time.</summary>
    [JsonPropertyName("status")] public string? Status { get; init; }

    /// <summary>Gets the human-readable order number.</summary>
    [JsonPropertyName("number")] public string? Number { get; init; }

    /// <summary>Gets the order total in minor currency units.</summary>
    [JsonPropertyName("amount")] public int? Amount { get; init; }

    /// <summary>Gets the tax amount in minor currency units.</summary>
    [JsonPropertyName("tax_amount")] public int? TaxAmount { get; init; }

    /// <summary>Gets the ISO 4217 currency code.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }

    /// <summary>Gets the order channel (e.g., <c>"web"</c>, <c>"api"</c>).</summary>
    [JsonPropertyName("channel")] public string? Channel { get; init; }

    /// <summary>Gets the billing reason (e.g., <c>"purchase"</c>, <c>"renewal"</c>).</summary>
    [JsonPropertyName("billing_reason")] public string? BillingReason { get; init; }

    /// <summary>Gets the associated customer.</summary>
    [JsonPropertyName("customer")] public WebhookCustomer? Customer { get; init; }

    /// <summary>Gets the associated subscription ID, if this order is a subscription renewal.</summary>
    [JsonPropertyName("subscription_id")] public string? SubscriptionId { get; init; }

    /// <summary>Gets the line items on this order.</summary>
    [JsonPropertyName("items")] public IReadOnlyList<WebhookOrderItem> Items { get; init; } = [];

    /// <summary>Gets the UTC creation timestamp.</summary>
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}

// ── Subscription ───────────────────────────────────────────────────────────

/// <summary>Payload data shared across all <c>subscription.*</c> webhook events.</summary>
public sealed record WebhookSubscriptionData
{
    /// <summary>Gets the Polar subscription ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Gets the subscription status at event time.</summary>
    [JsonPropertyName("status")] public string? Status { get; init; }

    /// <summary>Gets the current billing period start.</summary>
    [JsonPropertyName("current_period_start")] public DateTimeOffset? CurrentPeriodStart { get; init; }

    /// <summary>Gets the current billing period end.</summary>
    [JsonPropertyName("current_period_end")] public DateTimeOffset? CurrentPeriodEnd { get; init; }

    /// <summary>Gets the UTC timestamp when the subscription was canceled (if applicable).</summary>
    [JsonPropertyName("canceled_at")] public DateTimeOffset? CanceledAt { get; init; }

    /// <summary>Gets the UTC timestamp when the subscription ends (if applicable).</summary>
    [JsonPropertyName("ends_at")] public DateTimeOffset? EndsAt { get; init; }

    /// <summary>Gets the associated customer.</summary>
    [JsonPropertyName("customer")] public WebhookCustomer? Customer { get; init; }

    /// <summary>Gets the associated product.</summary>
    [JsonPropertyName("product")] public WebhookProduct? Product { get; init; }

    /// <summary>Gets the current price tier.</summary>
    [JsonPropertyName("price")] public WebhookPrice? Price { get; init; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}

// ── Checkout ───────────────────────────────────────────────────────────────

/// <summary>Payload data shared across all <c>checkout.*</c> webhook events.</summary>
public sealed record WebhookCheckoutData
{
    /// <summary>Gets the Polar checkout session ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Gets the checkout status at event time.</summary>
    [JsonPropertyName("status")] public string? Status { get; init; }

    /// <summary>Gets the checkout total in minor currency units.</summary>
    [JsonPropertyName("amount")] public int? Amount { get; init; }

    /// <summary>Gets the ISO 4217 currency code.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }

    /// <summary>Gets the customer email entered during checkout (may be null before customer enters email).</summary>
    [JsonPropertyName("customer_email")] public string? CustomerEmail { get; init; }

    /// <summary>Gets the associated customer (populated after customer is identified).</summary>
    [JsonPropertyName("customer")] public WebhookCustomer? Customer { get; init; }

    /// <summary>Gets the UTC expiry timestamp.</summary>
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets the resulting order ID once the checkout completes.</summary>
    [JsonPropertyName("order_id")] public string? OrderId { get; init; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}

// ── Customer ───────────────────────────────────────────────────────────────

/// <summary>Payload data shared across all <c>customer.*</c> webhook events.</summary>
public sealed record WebhookCustomerData
{
    /// <summary>Gets the Polar customer ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Gets the customer email address.</summary>
    [JsonPropertyName("email")] public string? Email { get; init; }

    /// <summary>Gets the customer display name.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Gets the organization the customer belongs to.</summary>
    [JsonPropertyName("organization_id")] public string? OrganizationId { get; init; }

    /// <summary>Gets the new customer state (for <c>customer.state_changed</c> events).</summary>
    [JsonPropertyName("active_subscriptions_count")] public int? ActiveSubscriptionsCount { get; init; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}

// ── Product ────────────────────────────────────────────────────────────────

/// <summary>Payload data shared across all <c>product.*</c> webhook events.</summary>
public sealed record WebhookProductData
{
    /// <summary>Gets the Polar product ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Gets the product display name.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Gets a value indicating whether the product is archived.</summary>
    [JsonPropertyName("is_archived")] public bool IsArchived { get; init; }

    /// <summary>Gets the organization that owns this product.</summary>
    [JsonPropertyName("organization_id")] public string? OrganizationId { get; init; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}

// ── Benefit ────────────────────────────────────────────────────────────────

/// <summary>Payload data shared across all <c>benefit.*</c> webhook events.</summary>
public sealed record WebhookBenefitData
{
    /// <summary>Gets the Polar benefit ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Gets the benefit type (e.g., <c>"custom"</c>, <c>"discord"</c>, <c>"license_keys"</c>).</summary>
    [JsonPropertyName("type")] public string? BenefitType { get; init; }

    /// <summary>Gets the benefit description.</summary>
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>Gets the organization that owns this benefit.</summary>
    [JsonPropertyName("organization_id")] public string? OrganizationId { get; init; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}

// ── Benefit Grant ──────────────────────────────────────────────────────────

/// <summary>Payload data shared across all <c>benefit_grant.*</c> webhook events.</summary>
public sealed record WebhookBenefitGrantData
{
    /// <summary>Gets the Polar benefit grant ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Gets the customer this grant applies to.</summary>
    [JsonPropertyName("customer_id")] public string? CustomerId { get; init; }

    /// <summary>Gets the benefit ID this grant is for.</summary>
    [JsonPropertyName("benefit_id")] public string? BenefitId { get; init; }

    /// <summary>Gets the benefit type string (e.g., <c>"discord"</c>).</summary>
    [JsonPropertyName("benefit_type")] public string? BenefitType { get; init; }

    /// <summary>Gets a value indicating whether the benefit is currently granted.</summary>
    [JsonPropertyName("is_granted")] public bool IsGranted { get; init; }

    /// <summary>Gets the UTC timestamp when the grant was awarded.</summary>
    [JsonPropertyName("granted_at")] public DateTimeOffset? GrantedAt { get; init; }

    /// <summary>Gets the UTC timestamp when the grant was revoked (if applicable).</summary>
    [JsonPropertyName("revoked_at")] public DateTimeOffset? RevokedAt { get; init; }
}

// ── Refund ─────────────────────────────────────────────────────────────────

/// <summary>Payload data shared across all <c>refund.*</c> webhook events.</summary>
public sealed record WebhookRefundData
{
    /// <summary>Gets the Polar refund ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Gets the refunded amount in minor currency units.</summary>
    [JsonPropertyName("amount")] public int? Amount { get; init; }

    /// <summary>Gets the ISO 4217 currency code.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }

    /// <summary>Gets the refund reason code.</summary>
    [JsonPropertyName("reason")] public string? Reason { get; init; }

    /// <summary>Gets the refund status (e.g., <c>"succeeded"</c>, <c>"pending"</c>).</summary>
    [JsonPropertyName("status")] public string? Status { get; init; }

    /// <summary>Gets the order ID that was refunded.</summary>
    [JsonPropertyName("order_id")] public string? OrderId { get; init; }

    /// <summary>Gets the customer ID who received the refund.</summary>
    [JsonPropertyName("customer_id")] public string? CustomerId { get; init; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}
