using PolarSharp.EcommerceStorefronts.Abstractions.Cart;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Customers;

/// <summary>A customer-saved address available for re-use at checkout.</summary>
/// <remarks>
/// Wraps a <see cref="ShippingAddress"/> with persistence metadata (identifier,
/// default-flag) so the customer-account UI can list and manage addresses.
/// </remarks>
public sealed record SavedAddress
{
    /// <summary>The saved address's identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Customer-visible nickname (for example <c>"Home"</c>, <c>"Office"</c>).</summary>
    public string? Nickname { get; init; }

    /// <summary>The underlying address.</summary>
    public required ShippingAddress Address { get; init; }

    /// <summary>True when this address is the customer's default shipping destination.</summary>
    public bool IsDefault { get; init; }
}
