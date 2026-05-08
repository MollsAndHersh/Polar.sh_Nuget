using System.Text;

namespace PolarSharp.Webhooks.Toast;

/// <summary>
/// Renders a <c>{Token}</c> template string by substituting values from a flat dictionary.
/// </summary>
/// <remarks>
/// AOT-safe: uses <see cref="StringBuilder.Replace(string,string)"/> — zero reflection,
/// zero expression compilation. Unknown tokens (not in the dictionary) are left as-is in
/// the rendered output.
/// </remarks>
internal static class ToastTemplateRenderer
{
    /// <summary>
    /// Renders <paramref name="template"/> by replacing each <c>{Key}</c> placeholder with
    /// the corresponding value from <paramref name="tokens"/>.
    /// </summary>
    /// <param name="template">
    /// The template string containing zero or more <c>{TokenName}</c> placeholders.
    /// </param>
    /// <param name="tokens">
    /// The token values to substitute. Keys are matched case-sensitively.
    /// </param>
    /// <returns>
    /// The rendered string. Unrecognized <c>{Tokens}</c> are left unchanged in the output.
    /// Returns an empty string when <paramref name="template"/> is <see langword="null"/> or empty.
    /// </returns>
    public static string Render(string template, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        ArgumentNullException.ThrowIfNull(tokens);

        var sb = new StringBuilder(template);
        foreach (var (key, value) in tokens)
            sb.Replace($"{{{key}}}", value);

        return sb.ToString();
    }
}
