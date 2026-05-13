using PolarSharp.BaseEntities;

namespace PolarSharp.BaseEntities.Tests;

/// <summary>
/// Verifies <see cref="PolarInventoryRecordBase.IsOutOfStock"/> and
/// <see cref="PolarInventoryRecordBase.IsLowStock"/> compute correctly.
/// </summary>
public sealed class InventoryRecordTests
{
    private sealed record TestInventory : PolarInventoryRecordBase;

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    [InlineData(100, false)]
    public void IsOutOfStock_reflects_OnHandCount(int onHand, bool expected)
    {
        var record = new TestInventory
        {
            SkuOrVariantId = "SKU-1",
            OnHandCount = onHand,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Assert.Equal(expected, record.IsOutOfStock);
    }

    [Theory]
    [InlineData(5, 10, true)]   // 5 on hand, 10 threshold ⇒ low
    [InlineData(10, 10, true)]  // exactly at threshold ⇒ low
    [InlineData(11, 10, false)] // above threshold ⇒ not low
    [InlineData(0, 10, true)]   // out of stock is also low
    public void IsLowStock_compares_against_threshold(int onHand, int threshold, bool expected)
    {
        var record = new TestInventory
        {
            SkuOrVariantId = "SKU-1",
            OnHandCount = onHand,
            LowThreshold = threshold,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Assert.Equal(expected, record.IsLowStock);
    }

    [Fact]
    public void IsLowStock_is_false_when_no_threshold_configured()
    {
        var record = new TestInventory
        {
            SkuOrVariantId = "SKU-1",
            OnHandCount = 0,
            LowThreshold = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Assert.False(record.IsLowStock);
    }
}
