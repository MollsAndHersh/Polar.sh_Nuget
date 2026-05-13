using PolarSharp.DataSeeding;

namespace PolarSharp.DataSeeding.Tests;

/// <summary>
/// Locks the documented per-scale row counts. Changing a count is a downstream-visible
/// change (test fixtures depend on it) so it should be deliberate.
/// </summary>
public sealed class SeedScaleTests
{
    [Fact]
    public void Demo_scale_uses_small_volumes_suitable_for_a_screencast()
    {
        var c = SeedScaleCounts.For(SeedScale.Demo);
        Assert.Equal(10, c.CustomerCount);
        Assert.Equal(20, c.ProductCount);
        Assert.Equal(5, c.CategoryCount);
        Assert.Equal(3, c.DepartmentCount);
    }

    [Fact]
    public void QA_scale_uses_volumes_that_exercise_paging_and_filters()
    {
        var c = SeedScaleCounts.For(SeedScale.QA);
        Assert.Equal(200, c.CustomerCount);
        Assert.Equal(500, c.ProductCount);
        Assert.Equal(30, c.CategoryCount);
    }

    [Fact]
    public void Stress_scale_uses_volumes_that_hit_DB_index_paths()
    {
        var c = SeedScaleCounts.For(SeedScale.Stress);
        Assert.Equal(10_000, c.CustomerCount);
        Assert.Equal(50_000, c.ProductCount);
        Assert.Equal(200, c.CategoryCount);
    }

    [Fact]
    public void Unknown_scale_value_throws_with_named_argument()
    {
        var bogus = (SeedScale)99;
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => SeedScaleCounts.For(bogus));
        Assert.Equal("scale", ex.ParamName);
    }
}
