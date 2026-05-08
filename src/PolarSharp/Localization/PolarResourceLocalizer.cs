using Microsoft.Extensions.Localization;

namespace PolarSharp.Localization;

/// <summary>
/// The built-in <see cref="IPolarLocalizer"/> implementation, backed by embedded <c>.resx</c>
/// resource files for <c>en-US</c> and <c>es-MX</c>.
/// </summary>
/// <remarks>
/// Registered via <c>TryAddSingleton</c> — only active when the host application does not
/// register its own <see cref="IPolarLocalizer"/> implementation.
/// <para>
/// Culture is resolved from <c>CultureInfo.CurrentUICulture</c> at each indexer call —
/// no culture is captured on construction, making this safe as a <c>Singleton</c>.
/// </para>
/// </remarks>
internal sealed class PolarResourceLocalizer(IStringLocalizerFactory factory) : IPolarLocalizer
{
    // The localizer is created once and reused — culture resolution is per-call via CurrentUICulture.
    private readonly IStringLocalizer _inner =
        factory.Create("PolarMessages", typeof(PolarResourceLocalizer).Assembly.GetName().Name!);

    /// <inheritdoc/>
    public LocalizedString this[string key] => _inner[key];

    /// <inheritdoc/>
    public LocalizedString this[string key, params object[] arguments] => _inner[key, arguments];
}
