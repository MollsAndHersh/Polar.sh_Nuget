using Microsoft.AspNetCore.Http;

namespace PolarSharp.Extensions;

/// <summary>
/// Extension methods for converting <see cref="Result{TValue,PolarError}"/> and
/// <see cref="PolarError"/> to ASP.NET Core <see cref="IResult"/> responses.
/// </summary>
/// <remarks>
/// These extensions allow Minimal API endpoints to convert PolarSharp results directly
/// to the correct HTTP response without writing manual switch expressions at every call site.
/// <example>
/// <code>
/// app.MapGet("/orders/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
///     (await polar.Orders.GetAsync(id, ct))
///         .ToHttpResult(order => Results.Ok(order)));
/// </code>
/// </example>
/// </remarks>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a <see cref="Result{TValue,PolarError}"/> to an <see cref="IResult"/>.
    /// On success, calls <paramref name="onSuccess"/> with the value.
    /// On failure, converts the <see cref="PolarError"/> to the appropriate HTTP response.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <param name="onSuccess">A function that maps the success value to an <see cref="IResult"/>.</param>
    /// <returns>
    /// The <see cref="IResult"/> returned by <paramref name="onSuccess"/> on success,
    /// or the HTTP error response mapped from the <see cref="PolarError"/> on failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="onSuccess"/> is <see langword="null"/>.
    /// </exception>
    public static IResult ToHttpResult<T>(
        this Result<T, PolarError> result,
        Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.Match(onSuccess, static error => error.ToHttpResult());
    }

    /// <summary>
    /// Converts a <see cref="PolarError"/> to the appropriate ASP.NET Core <see cref="IResult"/>.
    /// </summary>
    /// <param name="error">The Polar error to convert.</param>
    /// <returns>
    /// <list type="table">
    ///   <item><term><see cref="NotFoundError"/></term><description>404 Not Found with the error message.</description></item>
    ///   <item><term><see cref="AuthenticationError"/></term><description>401 Unauthorized.</description></item>
    ///   <item><term><see cref="AuthorizationError"/></term><description>403 Forbidden.</description></item>
    ///   <item><term><see cref="ValidationError"/></term><description>422 Unprocessable Entity with per-field details.</description></item>
    ///   <item><term><see cref="RateLimitError"/></term><description>429 Too Many Requests.</description></item>
    ///   <item><term><see cref="ServerError"/></term><description>502 Bad Gateway problem detail.</description></item>
    ///   <item><term>Any other subtype</term><description>500 Internal Server Error problem detail.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public static IResult ToHttpResult(this PolarError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error switch
        {
            NotFoundError e => Results.NotFound(e.Message),
            AuthenticationError => Results.Unauthorized(),
            AuthorizationError => Results.Forbid(),
            ValidationError e => Results.ValidationProblem(
                e.Fields.ToDictionary(
                    f => f.Field,
                    f => new[] { f.Message }),
                statusCode: StatusCodes.Status422UnprocessableEntity),
            RateLimitError => Results.StatusCode(StatusCodes.Status429TooManyRequests),
            ServerError e => Results.Problem(
                detail: e.Message,
                statusCode: StatusCodes.Status502BadGateway),
            _ => Results.Problem(
                detail: error.Message,
                statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
