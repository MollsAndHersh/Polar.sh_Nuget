namespace PolarSharp.EcommerceStorefronts.Abstractions.Customers;

/// <summary>The customer's self-service profile — name, contact, locale preference.</summary>
public sealed record CustomerProfile
{
    /// <summary>The customer's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The customer's primary email.</summary>
    public required string Email { get; init; }

    /// <summary>The customer's display name; may be <see langword="null"/> for guest-converted accounts.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The customer's preferred language tag (BCP-47).</summary>
    public string? PreferredLanguage { get; init; }

    /// <summary>The customer's preferred currency (ISO 4217); drives storefront display.</summary>
    public string? PreferredCurrency { get; init; }

    /// <summary>UTC timestamp the customer account was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
