namespace PolarSharp;

/// <summary>
/// Represents the result of an operation that can either succeed with a value of <typeparamref name="TValue"/>
/// or fail with an error of <typeparamref name="TError"/>.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
/// <typeparam name="TError">The type of the failure error.</typeparam>
/// <remarks>
/// All PolarSharp resource methods return <c>Result&lt;TValue, PolarError&gt;</c>. Recoverable HTTP errors
/// (401, 403, 404, 422, 429) are never thrown as exceptions — they surface as typed <see cref="PolarError"/>
/// failure values.
/// <para>
/// Use <see cref="Map{TResult}"/> to transform a success value without unwrapping.
/// Use <see cref="Bind{TResult}"/> to chain Result-returning operations (short-circuits on failure).
/// Use <see cref="Match{TResult}"/> to pattern-match at the call site — the canonical dispatch point.
/// </para>
/// <para>Thread-safe as a <see langword="readonly"/> value type.</para>
/// </remarks>
/// <example>
/// <code>
/// // Minimal API endpoint — fully ZH-compliant, no try/catch:
/// app.MapGet("/orders/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
///     (await polar.Orders.GetAsync(OrderId.From(id), ct))
///         .Match(
///             onSuccess: order => Results.Ok(order),
///             onFailure: error => error.ToHttpResult()));
/// </code>
/// </example>
public readonly record struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    private Result(TValue value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(TError error)
    {
        _error = error;
        IsSuccess = false;
    }

    /// <summary>Creates a successful <see cref="Result{TValue,TError}"/> containing <paramref name="value"/>.</summary>
    /// <param name="value">The success value. Must not be <see langword="null"/>.</param>
    /// <returns>A result in the success state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static Result<TValue, TError> Success(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<TValue, TError>(value);
    }

    /// <summary>Creates a failed <see cref="Result{TValue,TError}"/> containing <paramref name="error"/>.</summary>
    /// <param name="error">The error value. Must not be <see langword="null"/>.</param>
    /// <returns>A result in the failure state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public static Result<TValue, TError> Failure(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TValue, TError>(error);
    }

    /// <summary>
    /// Transforms the success value using <paramref name="mapper"/>; passes failure through unchanged.
    /// </summary>
    /// <typeparam name="TResult">The type of the mapped success value.</typeparam>
    /// <param name="mapper">A function to apply to the success value.</param>
    /// <returns>
    /// A <see cref="Result{TResult,TError}"/> in the success state with the mapped value,
    /// or the original failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapper"/> is <see langword="null"/>.</exception>
    public Result<TResult, TError> Map<TResult>(Func<TValue, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsSuccess
            ? Result<TResult, TError>.Success(mapper(_value!))
            : Result<TResult, TError>.Failure(_error!);
    }

    /// <summary>
    /// Chains a <see cref="Result{TValue,TError}"/>-returning operation; short-circuits on failure.
    /// </summary>
    /// <typeparam name="TResult">The type of the chained success value.</typeparam>
    /// <param name="binder">A function that takes the success value and returns a new result.</param>
    /// <returns>The result of <paramref name="binder"/> on success; the original failure on failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is <see langword="null"/>.</exception>
    public Result<TResult, TError> Bind<TResult>(Func<TValue, Result<TResult, TError>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder(_value!) : Result<TResult, TError>.Failure(_error!);
    }

    /// <summary>
    /// Asynchronously chains a <see cref="Result{TValue,TError}"/>-returning operation; short-circuits on failure.
    /// </summary>
    /// <typeparam name="TResult">The type of the chained success value.</typeparam>
    /// <param name="binder">An async function that takes the success value and returns a new result.</param>
    /// <returns>A task representing the result of <paramref name="binder"/> on success; the original failure on failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is <see langword="null"/>.</exception>
    public Task<Result<TResult, TError>> BindAsync<TResult>(
        Func<TValue, Task<Result<TResult, TError>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess
            ? binder(_value!)
            : Task.FromResult(Result<TResult, TError>.Failure(_error!));
    }

    /// <summary>
    /// Pattern-matches this result, invoking <paramref name="onSuccess"/> on success
    /// or <paramref name="onFailure"/> on failure.
    /// </summary>
    /// <typeparam name="TResult">The type of the matched result.</typeparam>
    /// <param name="onSuccess">A function applied to the success value.</param>
    /// <param name="onFailure">A function applied to the error value.</param>
    /// <returns>The result of either <paramref name="onSuccess"/> or <paramref name="onFailure"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>.
    /// </exception>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <summary>
    /// Asynchronously pattern-matches this result, invoking <paramref name="onSuccess"/> on success
    /// or <paramref name="onFailure"/> on failure.
    /// </summary>
    /// <typeparam name="TResult">The type of the matched result.</typeparam>
    /// <param name="onSuccess">An async function applied to the success value.</param>
    /// <param name="onFailure">An async function applied to the error value.</param>
    /// <returns>A task representing the result of either delegate.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>.
    /// </exception>
    public Task<TResult> MatchAsync<TResult>(
        Func<TValue, Task<TResult>> onSuccess,
        Func<TError, Task<TResult>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        IsSuccess ? $"Success({_value})" : $"Failure({_error})";
}
