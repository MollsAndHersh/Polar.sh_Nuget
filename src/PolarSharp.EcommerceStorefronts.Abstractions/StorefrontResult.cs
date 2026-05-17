namespace PolarSharp.EcommerceStorefronts.Abstractions;

/// <summary>
/// Represents the outcome of a storefront operation — either a success carrying a
/// <typeparamref name="TValue"/> or a failure carrying a <see cref="StorefrontError"/>.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
/// <remarks>
/// Lift-safe: this type is defined inside the storefront-core abstractions so that the
/// abstractions package can declare service signatures without taking a dependency on
/// the broader <c>PolarSharp</c> assembly's <c>Result&lt;TValue, PolarError&gt;</c> type.
/// Bridges that adapt PolarSharp internals into the storefront pipeline translate
/// <c>PolarError</c> values into <see cref="StorefrontError"/> at the bridge boundary.
/// <para>
/// The type is a readonly value record so that allocation is cheap and pattern matching
/// is exhaustive.
/// </para>
/// </remarks>
public readonly record struct StorefrontResult<TValue>
{
    private readonly TValue? _value;
    private readonly StorefrontError? _error;

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    private StorefrontResult(TValue value)
    {
        _value = value;
        IsSuccess = true;
    }

    private StorefrontResult(StorefrontError error)
    {
        _error = error;
        IsSuccess = false;
    }

    /// <summary>Creates a successful <see cref="StorefrontResult{TValue}"/> wrapping <paramref name="value"/>.</summary>
    /// <param name="value">The success value. Must not be <see langword="null"/>.</param>
    /// <returns>A result in the success state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static StorefrontResult<TValue> Success(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new StorefrontResult<TValue>(value);
    }

    /// <summary>Creates a failed <see cref="StorefrontResult{TValue}"/> wrapping <paramref name="error"/>.</summary>
    /// <param name="error">The error value. Must not be <see langword="null"/>.</param>
    /// <returns>A result in the failure state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public static StorefrontResult<TValue> Failure(StorefrontError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new StorefrontResult<TValue>(error);
    }

    /// <summary>
    /// Pattern-matches this result, invoking <paramref name="onSuccess"/> on success
    /// or <paramref name="onFailure"/> on failure.
    /// </summary>
    /// <typeparam name="TResult">The pattern-match return type.</typeparam>
    /// <param name="onSuccess">A function applied to the success value.</param>
    /// <param name="onFailure">A function applied to the failure value.</param>
    /// <returns>The result of either <paramref name="onSuccess"/> or <paramref name="onFailure"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>.
    /// </exception>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<StorefrontError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }
}
