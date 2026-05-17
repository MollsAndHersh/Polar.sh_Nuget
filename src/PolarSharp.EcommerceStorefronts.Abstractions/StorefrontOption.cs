namespace PolarSharp.EcommerceStorefronts.Abstractions;

/// <summary>
/// Represents an optional <typeparamref name="T"/> value — either present
/// (<see cref="Some(T)"/>) or absent (<see cref="None"/>).
/// </summary>
/// <typeparam name="T">The type of the contained value.</typeparam>
/// <remarks>
/// Lift-safe: the abstractions package owns this type so storefront service contracts
/// avoid depending on the broader <c>PolarSharp</c> assembly's <c>Option&lt;T&gt;</c>.
/// Use <see cref="Match{TResult}"/> for branch-free dispatch at the call site.
/// </remarks>
public readonly record struct StorefrontOption<T>
{
    private readonly T? _value;

    /// <summary>Gets a value indicating whether the option carries a value.</summary>
    public bool HasValue { get; }

    private StorefrontOption(T value)
    {
        _value = value;
        HasValue = true;
    }

    /// <summary>Creates an option containing <paramref name="value"/>.</summary>
    /// <param name="value">The value to wrap. Must not be <see langword="null"/>.</param>
    /// <returns>An option in the present state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static StorefrontOption<T> Some(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new StorefrontOption<T>(value);
    }

    /// <summary>The singleton absent option.</summary>
    public static readonly StorefrontOption<T> None = default;

    /// <summary>
    /// Pattern-matches this option, invoking <paramref name="onSome"/> when present
    /// or <paramref name="onNone"/> when absent.
    /// </summary>
    /// <typeparam name="TResult">The pattern-match return type.</typeparam>
    /// <param name="onSome">Function applied to the contained value when present.</param>
    /// <param name="onNone">Function invoked when absent.</param>
    /// <returns>The result of <paramref name="onSome"/> or <paramref name="onNone"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="onSome"/> or <paramref name="onNone"/> is <see langword="null"/>.
    /// </exception>
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
    {
        ArgumentNullException.ThrowIfNull(onSome);
        ArgumentNullException.ThrowIfNull(onNone);
        return HasValue ? onSome(_value!) : onNone();
    }

    /// <summary>Returns the contained value, or <paramref name="fallback"/> if absent.</summary>
    /// <param name="fallback">The value to return when absent.</param>
    /// <returns>The contained value, or <paramref name="fallback"/>.</returns>
    public T GetValueOrDefault(T fallback) => HasValue ? _value! : fallback;
}
