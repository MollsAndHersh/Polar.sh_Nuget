using PolarSharp.EcommerceStorefronts.Abstractions.Cart;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Shipping;

/// <summary>Request for shipping-rate quotes against a specific shipment.</summary>
public sealed record GetRatesRequest
{
    /// <summary>The address the package will ship from.</summary>
    public required ShippingAddress FromAddress { get; init; }

    /// <summary>The destination address.</summary>
    public required ShippingAddress ToAddress { get; init; }

    /// <summary>Package weight in grams.</summary>
    public required int WeightGrams { get; init; }

    /// <summary>Package length in centimetres; <see langword="null"/> when the carrier infers it.</summary>
    public int? LengthCm { get; init; }

    /// <summary>Package width in centimetres.</summary>
    public int? WidthCm { get; init; }

    /// <summary>Package height in centimetres.</summary>
    public int? HeightCm { get; init; }

    /// <summary>Declared value of the contents in minor units (for insurance / customs).</summary>
    public int? DeclaredValueCents { get; init; }

    /// <summary>ISO 4217 currency code for <see cref="DeclaredValueCents"/>.</summary>
    public string? DeclaredValueCurrency { get; init; }
}
