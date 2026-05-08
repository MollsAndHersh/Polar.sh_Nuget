namespace PolarSharp;

/// <summary>
/// Base type for all recoverable Polar API errors. Returned as the failure value of
/// <see cref="Result{TValue,TError}"/> — never thrown as an exception.
/// </summary>
/// <param name="Message">A human-readable, localized description of the error.</param>
/// <param name="RequestId">
/// The Polar request correlation ID from the <c>x-request-id</c> response header.
/// Include this value in support requests.
/// </param>
/// <remarks>
/// Use pattern matching on the concrete subtypes to handle each error category:
/// <code>
/// result.Match(
///     onSuccess: order => Results.Ok(order),
///     onFailure: error => error switch
///     {
///         NotFoundError e      => Results.NotFound(e.Message),
///         AuthenticationError  => Results.Unauthorized(),
///         AuthorizationError   => Results.Forbid(),
///         ValidationError e    => Results.ValidationProblem(...),
///         RateLimitError e     => Results.StatusCode(429),
///         _                    => Results.Problem(error.Message)
///     });
/// </code>
/// </remarks>
public abstract record PolarError(string Message, string RequestId);

/// <summary>
/// HTTP 401 — the access token is missing, invalid, or expired.
/// </summary>
/// <param name="Message">A human-readable description of the authentication failure.</param>
/// <param name="RequestId">Polar request correlation ID for diagnostics.</param>
public sealed record AuthenticationError(string Message, string RequestId)
    : PolarError(Message, RequestId);

/// <summary>
/// HTTP 403 — the access token is valid but lacks permission for the requested operation.
/// </summary>
/// <param name="Message">A human-readable description of the authorization failure.</param>
/// <param name="RequestId">Polar request correlation ID for diagnostics.</param>
public sealed record AuthorizationError(string Message, string RequestId)
    : PolarError(Message, RequestId);

/// <summary>
/// HTTP 404 — the requested Polar resource does not exist.
/// </summary>
/// <param name="Message">A human-readable description of what was not found.</param>
/// <param name="RequestId">Polar request correlation ID for diagnostics.</param>
public sealed record NotFoundError(string Message, string RequestId)
    : PolarError(Message, RequestId);

/// <summary>
/// HTTP 422 — the request was well-formed but failed Polar's validation rules.
/// Inspect <see cref="Fields"/> for per-field error details.
/// </summary>
/// <param name="Message">A human-readable summary of the validation failure.</param>
/// <param name="RequestId">Polar request correlation ID for diagnostics.</param>
/// <param name="Fields">Per-field validation errors, one entry per failing field.</param>
public sealed record ValidationError(
    string Message,
    string RequestId,
    IReadOnlyList<FieldValidationError> Fields)
    : PolarError(Message, RequestId);

/// <summary>
/// HTTP 429 — rate limit exceeded. Retries are already exhausted by the resilience handler.
/// </summary>
/// <param name="Message">A human-readable description of the rate limit.</param>
/// <param name="RequestId">Polar request correlation ID for diagnostics.</param>
/// <param name="RetryAfter">The duration to wait before the next request.</param>
public sealed record RateLimitError(string Message, string RequestId, TimeSpan RetryAfter)
    : PolarError(Message, RequestId);

/// <summary>
/// HTTP 5xx — an unrecoverable Polar server error after all retry attempts are exhausted.
/// </summary>
/// <param name="Message">A human-readable description of the server error.</param>
/// <param name="RequestId">Polar request correlation ID for diagnostics.</param>
public sealed record ServerError(string Message, string RequestId)
    : PolarError(Message, RequestId);

/// <summary>
/// Represents a validation error for a single field in a <see cref="ValidationError"/>.
/// </summary>
/// <param name="Field">The name of the failing field (as reported by Polar's API).</param>
/// <param name="Message">A human-readable description of why the field failed validation.</param>
public sealed record FieldValidationError(string Field, string Message);
