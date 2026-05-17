namespace PolarSharp.EcommerceStorefronts.Abstractions.Checkout;

/// <summary>
/// A pending checkout — created from a cart, threaded through the order pipeline.
/// </summary>
/// <remarks>
/// Carries enough context for the storefront UI to render a checkout page (cart totals,
/// status) and to resume after a redirect (for hosted payment flows). The full payment
/// + fulfillment data is filled in by the pipeline stages.
/// </remarks>
public sealed record CheckoutSession
{
    /// <summary>The session identifier passed to <see cref="IStorefrontCheckoutService.ProcessCheckoutAsync"/>.</summary>
    public required Guid Id { get; init; }

    /// <summary>The cart this session was initiated from.</summary>
    public required Guid CartId { get; init; }

    /// <summary>The customer placing the order; <see langword="null"/> for guest checkout.</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>The tenant scope; <see langword="null"/> in single-tenant deployments.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>The current pipeline status.</summary>
    public required CheckoutStatus Status { get; init; }

    /// <summary>UTC timestamp the session was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC timestamp the session reached a terminal state; <see langword="null"/> until then.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Optional URL the storefront should send the customer to in order to finish the
    /// checkout — populated when the pipeline yields control to a hosted payment page.
    /// </summary>
    public string? RedirectUrl { get; init; }

    /// <summary>The Polar order identifier once <see cref="CheckoutStatus.Completed"/>.</summary>
    public string? OrderId { get; init; }
}
