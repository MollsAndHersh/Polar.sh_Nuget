namespace PolarSharp.EcommerceStorefronts.Abstractions.Cart;

/// <summary>
/// Shipping address attached to a cart or order. Modelled as an immutable record so
/// addresses can be safely snapshotted onto orders without later mutation.
/// </summary>
/// <remarks>
/// Mirrors <c>PolarSharp.BaseEntities.PolarAddressBase</c> in shape but is decoupled to
/// keep the abstractions package free of inheritance ties; the shape is intentionally
/// a flat record so address-validation providers can read it without LINQ.
/// </remarks>
public sealed record ShippingAddress
{
    /// <summary>Recipient full name.</summary>
    public required string FullName { get; init; }

    /// <summary>First line of the street address.</summary>
    public required string Line1 { get; init; }

    /// <summary>Second line of the street address (apartment, suite, etc.).</summary>
    public string? Line2 { get; init; }

    /// <summary>City / locality.</summary>
    public required string City { get; init; }

    /// <summary>State / province / administrative region.</summary>
    public string? Region { get; init; }

    /// <summary>Postal or ZIP code.</summary>
    public required string PostalCode { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Optional phone number (used by some carriers for delivery scheduling).</summary>
    public string? Phone { get; init; }
}
