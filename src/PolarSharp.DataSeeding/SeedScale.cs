namespace PolarSharp.DataSeeding;

/// <summary>
/// Preset volumes for <c>IPolarDataSeeder.SeedFullCatalogAsync</c>. Each level roughly 10×
/// the previous.
/// </summary>
public enum SeedScale
{
    /// <summary>Demo-friendly volumes — 10 customers, 20 products, 5 categories, etc.</summary>
    Demo,
    /// <summary>QA-friendly volumes — 200 customers, 500 products with variants, 30 categories, etc.</summary>
    QA,
    /// <summary>Stress / load-test volumes — 10,000 customers, 50,000 products, etc.</summary>
    Stress,
}

/// <summary>Per-scale row counts. Internal — exposed as <see cref="SeedScaleCounts.For"/>.</summary>
public sealed record SeedScaleCounts(
    int CustomerCount,
    int ProductCount,
    int CategoryCount,
    int DepartmentCount,
    int TierGroupCount,
    int BenefitsPerKind,
    int DiscountCount,
    int CheckoutLinkCount)
{
    /// <summary>Looks up the row counts for the given scale.</summary>
    public static SeedScaleCounts For(SeedScale scale) => scale switch
    {
        SeedScale.Demo   => new(10,   20,    5,  3,   2,  2,   3,  2),
        SeedScale.QA     => new(200,  500,   30, 10,  10, 5,   20, 10),
        SeedScale.Stress => new(10_000, 50_000, 200, 30, 50, 10, 100, 50),
        _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, "Unknown seed scale."),
    };
}
