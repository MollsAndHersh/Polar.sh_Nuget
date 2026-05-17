namespace PolarSharp.EcommerceStorefronts.Abstractions.Customers;

/// <summary>Command to update a customer's profile fields. Only non-null fields are applied.</summary>
public sealed record UpdateProfileCommand
{
    /// <summary>New display name; <see langword="null"/> leaves the existing value untouched.</summary>
    public string? DisplayName { get; init; }

    /// <summary>New preferred language tag (BCP-47); <see langword="null"/> leaves untouched.</summary>
    public string? PreferredLanguage { get; init; }

    /// <summary>New preferred currency (ISO 4217); <see langword="null"/> leaves untouched.</summary>
    public string? PreferredCurrency { get; init; }
}
