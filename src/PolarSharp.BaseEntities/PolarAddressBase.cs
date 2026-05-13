namespace PolarSharp.BaseEntities;

/// <summary>
/// Postal-address shape used across Polar.sh entities (customer billing address, organization
/// business address, etc.). All fields are optional — Polar uses partial addresses freely.
/// </summary>
/// <remarks>
/// <para>
/// Polar emits this as a nested object inside larger entities (e.g. <c>customer.billing_address</c>,
/// <c>order.billing_address</c>). The <see cref="Country"/> field is ISO 3166-1 alpha-2.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed record MyAddress : PolarAddressBase
/// {
///     public string? FormattedDisplay =&gt; $"{Line1}, {City}, {State} {PostalCode}";
/// }
/// </code>
/// </example>
public abstract record PolarAddressBase
{
    /// <summary>Gets or initializes the first line of the street address.</summary>
    public string? Line1 { get; init; }

    /// <summary>Gets or initializes the second line of the street address (apt, suite, unit, etc.).</summary>
    public string? Line2 { get; init; }

    /// <summary>Gets or initializes the city / locality.</summary>
    public string? City { get; init; }

    /// <summary>Gets or initializes the state / province / administrative region.</summary>
    public string? State { get; init; }

    /// <summary>Gets or initializes the postal / ZIP code.</summary>
    public string? PostalCode { get; init; }

    /// <summary>Gets or initializes the country code (ISO 3166-1 alpha-2, e.g. "US", "GB", "MX").</summary>
    public string? Country { get; init; }
}
