namespace PolarSharp.EcommerceStoreManagement.Cloning;

/// <summary>Clones a <see cref="LocalCheckoutLinkConfig"/>. Polar-side state is reset; the source's <c>ProductIds</c> list is copied so the clone sells the same products by default.</summary>
public interface ICheckoutLinkCloningService
{
    /// <summary>Clones the checkout link.</summary>
    Task<Result<LocalCheckoutLinkConfig, CloningError>> CloneAsync(
        CheckoutLinkId source,
        CloneCheckoutLinkOverrides? overrides = null,
        CloneCheckoutLinkOptions? options = null,
        CancellationToken ct = default);
}

/// <summary>Field overrides for a checkout-link clone.</summary>
public sealed record CloneCheckoutLinkOverrides
{
    /// <summary>Override the new link's host-side display name.</summary>
    public string? NewName { get; init; }
    /// <summary>Replace the product list. <see langword="null"/> = copy from source.</summary>
    public IReadOnlyList<ProductId>? NewProductIds { get; init; }
    /// <summary>Override the success URL.</summary>
    public string? NewSuccessUrl { get; init; }
    /// <summary>Override the cancel URL.</summary>
    public string? NewCancelUrl { get; init; }
}

/// <summary>Cascade toggles for a checkout-link clone.</summary>
public sealed record CloneCheckoutLinkOptions
{
    /// <summary>When <see langword="true"/>, the <c>CustomFields</c> list is copied. Default <see langword="true"/>.</summary>
    public bool IncludeCustomFields { get; init; } = true;
}
