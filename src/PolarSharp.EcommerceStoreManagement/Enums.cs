namespace PolarSharp.EcommerceStoreManagement;

/// <summary>What kind of thing a <see cref="LocalProduct"/> represents.</summary>
public enum ProductKind
{
    /// <summary>A physical or digital good with quantifiable units.</summary>
    Product,

    /// <summary>A service — typically time- or usage-based, not stocked.</summary>
    Service,
}

/// <summary>Status of a local catalog entity's mirror in Polar.</summary>
public enum PublishStatus
{
    /// <summary>Authored locally; never published to Polar.</summary>
    Draft,

    /// <summary>Successfully published; <c>PolarXxxId</c> populated.</summary>
    Published,

    /// <summary>Published once, but the local copy has diverged since the last publish. Re-publish to reconcile.</summary>
    OutOfSync,

    /// <summary>The most recent publish attempt failed. See audit log for the error.</summary>
    PublishFailed,
}

/// <summary>Which Polar pricing shape a <see cref="LocalPrice"/> maps to.</summary>
public enum PriceKind
{
    /// <summary>Fixed-amount one-time or recurring price.</summary>
    Fixed,

    /// <summary>Customer-chosen "pay what you want" price within an optional range.</summary>
    Custom,

    /// <summary>Free.</summary>
    Free,

    /// <summary>Per-unit consumption priced against a metered counter.</summary>
    MeteredUnit,

    /// <summary>Seat-tiered pricing (Polar's "tiered pricing" semantics: 1-10 seats @ $X, 11-50 @ $Y).</summary>
    SeatBased,
}

/// <summary>Local discount kind — maps to Polar's <c>type</c> field.</summary>
public enum DiscountKind
{
    /// <summary>Percentage-off discount.</summary>
    Percentage,

    /// <summary>Fixed-amount-off discount.</summary>
    Fixed,
}

/// <summary>How long a discount applies to a subscription.</summary>
public enum DiscountDuration
{
    /// <summary>Applies indefinitely.</summary>
    Forever,

    /// <summary>Applies to the first invoice only.</summary>
    Once,

    /// <summary>Applies for a fixed number of months (set <c>DurationInMonths</c>).</summary>
    Repeating,
}

/// <summary>Polar's tax behaviour for an organization.</summary>
public enum DefaultTaxBehavior
{
    /// <summary>Tax determined by the customer's billing location.</summary>
    Location,

    /// <summary>Prices include tax.</summary>
    Inclusive,

    /// <summary>Tax is added on top of prices.</summary>
    Exclusive,
}

/// <summary>Status of a tenant's Stripe Connect / payout setup.</summary>
public enum PayoutSetupStatus
{
    /// <summary>Stripe Connect linking has not been started for this organization.</summary>
    NotStarted,

    /// <summary>Linking is in progress — the merchant has begun the dashboard flow but not finished.</summary>
    InProgress,

    /// <summary>Linking is complete — the organization can receive payouts.</summary>
    Ready,
}

/// <summary>AI translation provider selection for a tenant.</summary>
public enum TranslationProvider
{
    /// <summary>No per-tenant translation. Falls back to the master/SaaS-site config, or disabled if that's also missing.</summary>
    None,

    /// <summary>Anthropic Claude.</summary>
    Anthropic,

    /// <summary>OpenAI GPT.</summary>
    OpenAI,

    /// <summary>Azure OpenAI Service.</summary>
    AzureOpenAI,

    /// <summary>Google Gemini.</summary>
    Gemini,

    /// <summary>xAI Grok.</summary>
    Grok,
}

/// <summary>License key format options for the <c>LicenseKeysBenefit</c>.</summary>
public enum LicenseKeyFormat
{
    /// <summary>UUID v4 format.</summary>
    Uuid,

    /// <summary>16 hex characters.</summary>
    Hex16,

    /// <summary>32 hex characters.</summary>
    Hex32,

    /// <summary>Host-defined custom format — supply a generator via <c>ILicenseKeyGenerator</c>.</summary>
    Custom,
}

/// <summary>GitHub repository access level granted by a <c>GitHubRepoBenefit</c>.</summary>
public enum GitHubRepoPermission
{
    /// <summary>Pull access — read-only.</summary>
    Pull,

    /// <summary>Triage — read + manage issues / PRs (no code write).</summary>
    Triage,

    /// <summary>Push — write access to non-protected branches.</summary>
    Push,

    /// <summary>Maintain — write + repo settings (no admin).</summary>
    Maintain,

    /// <summary>Admin — full control of the repository.</summary>
    Admin,
}

/// <summary>Polar checkout custom-field input kind.</summary>
public enum CustomFieldKind
{
    /// <summary>Free-form text.</summary>
    Text,

    /// <summary>Numeric value.</summary>
    Number,

    /// <summary>Date picker.</summary>
    Date,

    /// <summary>Boolean checkbox.</summary>
    Checkbox,

    /// <summary>Single-select from a predefined list.</summary>
    Select,
}

/// <summary>Refund reason codes (mirror Polar's <c>refund_reason</c> enum).</summary>
public enum RefundReason
{
    /// <summary>Customer requested the refund (most common).</summary>
    CustomerRequest,

    /// <summary>The same charge was processed more than once.</summary>
    DuplicateCharge,

    /// <summary>The original charge was fraudulent.</summary>
    Fraudulent,

    /// <summary>The customer did not receive the product.</summary>
    ProductNotReceived,

    /// <summary>The product was unacceptable on arrival.</summary>
    ProductUnacceptable,

    /// <summary>Other — should be paired with a free-form comment.</summary>
    Other,
}

/// <summary>The kind of mutation captured by an <see cref="AdminAuditLogEntry"/>.</summary>
public enum AuditAction
{
    /// <summary>A new entity was created.</summary>
    Create,

    /// <summary>An existing entity was updated.</summary>
    Update,

    /// <summary>An entity was deleted (or soft-deleted).</summary>
    Delete,
}
