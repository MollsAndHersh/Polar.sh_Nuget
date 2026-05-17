namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// The customer-facing business profile for the storefront — merchant name, support
/// contact, branding, legal links. Rendered in footer + checkout + receipt surfaces.
/// </summary>
/// <remarks>
/// In multi-tenant deployments the profile is resolved per tenant. The catalog provider
/// is the canonical source — it bridges to <c>PolarSharp.EcommerceStoreManagement</c>'s
/// business-profile service or to a host-supplied implementation.
/// </remarks>
public sealed record StorefrontBusinessProfile
{
    /// <summary>The legal/trading name shown to customers.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Public support email address.</summary>
    public string? SupportEmail { get; init; }

    /// <summary>Public support phone number.</summary>
    public string? SupportPhone { get; init; }

    /// <summary>URL of the merchant's logo (suitable for the storefront header).</summary>
    public string? LogoUrl { get; init; }

    /// <summary>Primary brand colour as a CSS hex string (for example <c>"#0a84ff"</c>).</summary>
    public string? PrimaryColorHex { get; init; }

    /// <summary>URL of the merchant's terms of service page.</summary>
    public string? TermsUrl { get; init; }

    /// <summary>URL of the merchant's privacy policy page.</summary>
    public string? PrivacyUrl { get; init; }

    /// <summary>URL of the merchant's refund policy page.</summary>
    public string? RefundPolicyUrl { get; init; }
}
