namespace PolarSharp.EcommerceStorefronts.Abstractions.Customers;

/// <summary>The customer's prepaid wallet balance, when the host has prepaid wallets enabled.</summary>
/// <remarks>
/// Surfaced to storefront customer-account pages so the wallet balance is visible at
/// checkout time. Bridges from <c>PolarSharp.PrepaidWallets</c>; guest sessions resolve
/// to a zero balance rather than an error so call sites avoid branching on guest state.
/// </remarks>
public sealed record WalletBalance
{
    /// <summary>The customer's wallet identifier; <see langword="null"/> when no wallet exists yet.</summary>
    public Guid? WalletId { get; init; }

    /// <summary>Available balance in minor units.</summary>
    public required int AvailableCents { get; init; }

    /// <summary>Reserved balance (held by in-flight orders) in minor units.</summary>
    public int ReservedCents { get; init; }

    /// <summary>ISO 4217 currency code; wallets are single-currency.</summary>
    public required string Currency { get; init; }

    /// <summary>UTC timestamp the balance was sampled at.</summary>
    public required DateTimeOffset AsOf { get; init; }
}
