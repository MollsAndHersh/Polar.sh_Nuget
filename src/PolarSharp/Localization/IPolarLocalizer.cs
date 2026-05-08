using Microsoft.Extensions.Localization;

namespace PolarSharp.Localization;

/// <summary>
/// Provides localized string lookup for PolarSharp messages.
/// </summary>
/// <remarks>
/// The library registers its own <see cref="PolarResourceLocalizer"/> implementation via
/// <c>TryAddSingleton</c> — it is used automatically when no host implementation is registered.
/// <para>
/// To override with a custom source (database, translation API, etc.), register your implementation
/// <strong>before</strong> calling <c>AddPolarInfrastructure</c>:
/// </para>
/// <code>
/// services.AddSingleton&lt;IPolarLocalizer, MyDatabaseLocalizer&gt;();
/// services.AddPolarInfrastructure(builder.Configuration);
/// </code>
/// <para>
/// Registered as a <c>Singleton</c> — each indexer call reads <c>CultureInfo.CurrentUICulture</c>
/// at call time, so it is thread-safe and culture-aware without storing any culture state.
/// </para>
/// </remarks>
public interface IPolarLocalizer
{
    /// <summary>Gets the localized string for <paramref name="key"/>.</summary>
    /// <param name="key">The message key constant from <see cref="PolarMessageKeys"/>.</param>
    /// <returns>
    /// The localized <see cref="LocalizedString"/>. If the key is not found, the returned string's
    /// <see cref="LocalizedString.ResourceNotFound"/> property is <see langword="true"/> and
    /// <see cref="LocalizedString.Value"/> contains the key itself as a fallback.
    /// </returns>
    LocalizedString this[string key] { get; }

    /// <summary>
    /// Gets the localized string for <paramref name="key"/>, substituting <paramref name="arguments"/>
    /// into any format placeholders.
    /// </summary>
    /// <param name="key">The message key constant.</param>
    /// <param name="arguments">Arguments to substitute into the localized format string.</param>
    /// <returns>The localized and formatted <see cref="LocalizedString"/>.</returns>
    LocalizedString this[string key, params object[] arguments] { get; }
}
