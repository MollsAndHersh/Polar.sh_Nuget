namespace PolarSharp;

/// <summary>
/// Represents an optional value — either a value is present (<see cref="Some(T)"/>) or absent (<see cref="None"/>).
/// Eliminates null checks and <see langword="null"/> propagation from the public API.
/// </summary>
/// <typeparam name="T">The type of the contained value.</typeparam>
/// <remarks>
/// Use <see cref="Map{TResult}"/> to transform the value without unwrapping.
/// Use <see cref="Bind{TResult}"/> to chain operations that also return <see cref="Option{T}"/>.
/// Use <see cref="Match{TResult}"/> to pattern-match at the call site without branching.
/// <para>Thread-safe as a <see langword="readonly"/> value type.</para>
/// </remarks>
public readonly record struct Option<T>
{
    private readonly T? _value;

    /// <summary>Gets a value indicating whether this option contains a value.</summary>
    public bool HasValue { get; }

    private Option(T value)
    {
        _value = value;
        HasValue = true;
    }

    /// <summary>Creates an <see cref="Option{T}"/> that contains <paramref name="value"/>.</summary>
    /// <param name="value">The value to wrap. Must not be <see langword="null"/>.</param>
    /// <returns>An <see cref="Option{T}"/> in the Some state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static Option<T> Some(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Option<T>(value);
    }

    /// <summary>Gets the singleton <see cref="Option{T}"/> that represents the absent state.</summary>
    public static readonly Option<T> None = default;

    /// <summary>
    /// Transforms the contained value using <paramref name="mapper"/>; returns <see cref="None"/> if absent.
    /// </summary>
    /// <typeparam name="TResult">The type of the mapped result.</typeparam>
    /// <param name="mapper">A function to apply to the contained value.</param>
    /// <returns>
    /// <see cref="Option{TResult}.Some(TResult)"/> wrapping the mapped value, or <see cref="Option{TResult}.None"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapper"/> is <see langword="null"/>.</exception>
    public Option<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return HasValue ? Option<TResult>.Some(mapper(_value!)) : Option<TResult>.None;
    }

    /// <summary>
    /// Chains an <see cref="Option{T}"/>-returning operation; returns <see cref="None"/> if this is absent.
    /// </summary>
    /// <typeparam name="TResult">The type of the result option.</typeparam>
    /// <param name="binder">A function that takes the contained value and returns a new option.</param>
    /// <returns>The result of <paramref name="binder"/> if present; otherwise <see cref="Option{TResult}.None"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is <see langword="null"/>.</exception>
    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return HasValue ? binder(_value!) : Option<TResult>.None;
    }

    /// <summary>
    /// Pattern-matches this option, invoking <paramref name="onSome"/> if a value is present
    /// or <paramref name="onNone"/> if absent.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="onSome">A function applied to the contained value when present.</param>
    /// <param name="onNone">A function invoked when no value is present.</param>
    /// <returns>The result of either <paramref name="onSome"/> or <paramref name="onNone"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="onSome"/> or <paramref name="onNone"/> is <see langword="null"/>.
    /// </exception>
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
    {
        ArgumentNullException.ThrowIfNull(onSome);
        ArgumentNullException.ThrowIfNull(onNone);
        return HasValue ? onSome(_value!) : onNone();
    }

    /// <summary>
    /// Returns the contained value, or <paramref name="defaultValue"/> if absent.
    /// </summary>
    /// <param name="defaultValue">The value to return when this option is absent.</param>
    /// <returns>The contained value or <paramref name="defaultValue"/>.</returns>
    public T GetValueOrDefault(T defaultValue) => HasValue ? _value! : defaultValue;

    /// <inheritdoc/>
    public override string ToString() => HasValue ? $"Some({_value})" : "None";
}
