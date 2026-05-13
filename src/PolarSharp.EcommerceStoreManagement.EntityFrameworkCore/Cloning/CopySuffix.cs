namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;

/// <summary>
/// Generates a non-colliding "X (Copy N)" name by probing an existence-check delegate.
/// Used by every cloning service to avoid <c>(tenant_id, name)</c> unique-index violations
/// before insert.
/// </summary>
internal static class CopySuffix
{
    /// <summary>Maximum number of suffix attempts before giving up.</summary>
    public const int MaxAttempts = 100;

    /// <summary>
    /// Returns the first name in the sequence <c>"{base} (Copy)"</c>, <c>"{base} (Copy 2)"</c>,
    /// <c>"{base} (Copy 3)"</c> … that <paramref name="existsAsync"/> reports as available.
    /// </summary>
    /// <param name="baseName">The source name to derive from.</param>
    /// <param name="existsAsync">Probe — receives a candidate and returns <see langword="true"/> when it's already taken.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The first non-colliding candidate, or <see langword="null"/> when no candidate fits within <see cref="MaxAttempts"/>.</returns>
    public static async Task<string?> NextAvailableAsync(
        string baseName,
        Func<string, CancellationToken, Task<bool>> existsAsync,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(existsAsync);

        var candidate = $"{baseName} (Copy)";
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (!await existsAsync(candidate, ct).ConfigureAwait(false)) return candidate;
            candidate = $"{baseName} (Copy {attempt + 1})";
        }
        return null;
    }
}
