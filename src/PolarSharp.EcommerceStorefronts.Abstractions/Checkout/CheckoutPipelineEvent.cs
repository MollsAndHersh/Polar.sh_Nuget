namespace PolarSharp.EcommerceStorefronts.Abstractions.Checkout;

/// <summary>
/// Discriminated union of events emitted from
/// <see cref="IStorefrontCheckoutService.ProcessCheckoutAsync"/> as the pipeline runs.
/// </summary>
/// <remarks>
/// The pipeline is observed as an <c>IAsyncEnumerable&lt;CheckoutPipelineEvent&gt;</c>
/// so the storefront UI can stream progress (toast notifications, hub broadcasts)
/// without polling. Use a switch expression on the concrete derivatives to dispatch.
/// </remarks>
public abstract record CheckoutPipelineEvent
{
    /// <summary>The session this event belongs to.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>UTC timestamp the event was emitted.</summary>
    public required DateTimeOffset At { get; init; }
}

/// <summary>Emitted when a stage begins running.</summary>
/// <remarks>Use to drive UI affordances such as "Quoting tax…" indicators.</remarks>
public sealed record CheckoutStageStarted : CheckoutPipelineEvent
{
    /// <summary>The stage that started.</summary>
    public required CheckoutStatus Stage { get; init; }
}

/// <summary>Emitted when a stage completes successfully.</summary>
public sealed record CheckoutStageCompleted : CheckoutPipelineEvent
{
    /// <summary>The stage that completed.</summary>
    public required CheckoutStatus Stage { get; init; }
}

/// <summary>Emitted when a stage fails. Terminal — no further events follow.</summary>
public sealed record CheckoutFailed : CheckoutPipelineEvent
{
    /// <summary>The stage that failed.</summary>
    public required CheckoutStatus Stage { get; init; }

    /// <summary>The failure value.</summary>
    public required StorefrontError Error { get; init; }
}

/// <summary>Emitted on successful pipeline completion. Terminal.</summary>
public sealed record CheckoutSucceeded : CheckoutPipelineEvent
{
    /// <summary>The Polar order identifier produced by the fulfillment stage.</summary>
    public required string OrderId { get; init; }
}
