namespace PolarSharp.DataSeeding;

/// <summary>Bound from <c>PolarSharp:DataSeeding</c> in <c>appsettings.json</c>.</summary>
public sealed class PolarDataSeedingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PolarSharp:DataSeeding";

    /// <summary>When false, <see cref="Sync.FakeDataSyncService"/> is not registered. Default true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Default scale for <c>SeedFullCatalogAsync</c> when the caller doesn't specify. Default <see cref="SeedScale.QA"/>.</summary>
    public SeedScale DefaultScale { get; set; } = SeedScale.QA;

    /// <summary>When true, AllowFakeData toggle changes trigger the publish-to-Polar / archive-in-Polar sync. Default true.</summary>
    public bool AutoSyncOnToggleChange { get; set; } = true;

    /// <summary>Bounded parallelism — concurrent publish calls when reconciling fake data with Polar. Default 8.</summary>
    public int MaxConcurrentPublishes { get; set; } = 8;

    /// <summary>Capacity of the toggle-change channel. Events beyond this are dropped. Default 256.</summary>
    public int ToggleChannelCapacity { get; set; } = 256;

    /// <summary>Default locale for Bogus generators (currently only en-US is provided by Bogus's built-in datasets).</summary>
    public string DefaultLocale { get; set; } = "en-US";
}
