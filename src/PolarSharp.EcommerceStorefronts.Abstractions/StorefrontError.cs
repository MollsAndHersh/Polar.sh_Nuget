namespace PolarSharp.EcommerceStorefronts.Abstractions;

/// <summary>
/// Base type for failures returned from storefront operations. Carried as the failure
/// value of <see cref="StorefrontResult{TValue}"/> rather than thrown as an exception.
/// </summary>
/// <param name="Message">A human-readable, localizable description of the failure.</param>
/// <param name="CorrelationId">
/// An opaque identifier the host can echo to the customer for support correlation.
/// Bridges that translate from <c>PolarError</c> populate this from Polar's
/// <c>x-request-id</c> header.
/// </param>
/// <remarks>
/// Lift-safe: the abstractions package owns this type so hosts can implement storefront
/// services without depending on the broader <c>PolarSharp</c> assembly. Use the
/// concrete sub-records to dispatch on category.
/// </remarks>
public abstract record StorefrontError(string Message, string CorrelationId);

/// <summary>The caller is not authenticated and the operation requires authentication.</summary>
/// <param name="Message">A human-readable description.</param>
/// <param name="CorrelationId">Correlation identifier for support diagnostics.</param>
public sealed record StorefrontAuthenticationError(string Message, string CorrelationId)
    : StorefrontError(Message, CorrelationId);

/// <summary>The caller is authenticated but lacks permission for the operation.</summary>
/// <param name="Message">A human-readable description.</param>
/// <param name="CorrelationId">Correlation identifier for support diagnostics.</param>
public sealed record StorefrontAuthorizationError(string Message, string CorrelationId)
    : StorefrontError(Message, CorrelationId);

/// <summary>The requested entity (cart, product, order, address) was not found.</summary>
/// <param name="Message">A human-readable description.</param>
/// <param name="CorrelationId">Correlation identifier for support diagnostics.</param>
public sealed record StorefrontNotFoundError(string Message, string CorrelationId)
    : StorefrontError(Message, CorrelationId);

/// <summary>The request was well-formed but failed business validation.</summary>
/// <param name="Message">A human-readable summary of the validation failure.</param>
/// <param name="CorrelationId">Correlation identifier for support diagnostics.</param>
/// <param name="Fields">Per-field validation failures.</param>
public sealed record StorefrontValidationError(
    string Message,
    string CorrelationId,
    IReadOnlyList<StorefrontFieldError> Fields)
    : StorefrontError(Message, CorrelationId);

/// <summary>An upstream provider (catalog, payment, shipping, tax, etc.) reported a failure.</summary>
/// <param name="Message">A human-readable description.</param>
/// <param name="CorrelationId">Correlation identifier for support diagnostics.</param>
/// <param name="Provider">The provider that failed (for diagnostics, e.g. <c>"shipping:shippo"</c>).</param>
public sealed record StorefrontProviderError(string Message, string CorrelationId, string Provider)
    : StorefrontError(Message, CorrelationId);

/// <summary>A conflict prevented the operation (e.g. inventory ran out between add-to-cart and checkout).</summary>
/// <param name="Message">A human-readable description.</param>
/// <param name="CorrelationId">Correlation identifier for support diagnostics.</param>
public sealed record StorefrontConflictError(string Message, string CorrelationId)
    : StorefrontError(Message, CorrelationId);

/// <summary>Per-field validation failure within a <see cref="StorefrontValidationError"/>.</summary>
/// <param name="Field">The field that failed.</param>
/// <param name="Message">A human-readable description.</param>
public sealed record StorefrontFieldError(string Field, string Message);
