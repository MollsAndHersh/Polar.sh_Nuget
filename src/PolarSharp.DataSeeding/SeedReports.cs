namespace PolarSharp.DataSeeding;

/// <summary>Outcome of a seed operation.</summary>
public sealed record SeedReport(int Created, int Skipped, TimeSpan Duration, IReadOnlyList<string> Warnings)
{
    /// <summary>An empty report.</summary>
    public static SeedReport Empty { get; } = new(0, 0, TimeSpan.Zero, []);
}

/// <summary>Outcome of a fake-data cleanup operation.</summary>
public sealed record CleanupReport(int LocallyDeleted, int PolarArchived, TimeSpan Duration)
{
    /// <summary>An empty cleanup report.</summary>
    public static CleanupReport Empty { get; } = new(0, 0, TimeSpan.Zero);
}
