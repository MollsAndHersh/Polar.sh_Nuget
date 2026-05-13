using PolarSharp.BaseEntities;

namespace PolarSharp.BaseEntities.Tests;

/// <summary>
/// Verifies <see cref="PolarSaleBase.ComputeStatus"/> correctly transitions Pending → Active
/// → Ended based on StartsAt / EndsAt and the supplied current time.
/// </summary>
public sealed class SaleStatusComputationTests
{
    private sealed record TestSale : PolarSaleBase;

    private static TestSale Make(DateTimeOffset? startsAt, DateTimeOffset? endsAt) => new()
    {
        Id = "sale_1",
        Name = "Test",
        Type = "percentage",
        OrganizationId = "org_1",
        CreatedAt = DateTimeOffset.UtcNow,
        CampaignName = "Test Campaign",
        StartsAt = startsAt,
        EndsAt = endsAt,
    };

    [Fact]
    public void Pending_when_StartsAt_is_in_the_future()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var sale = Make(now.AddDays(7), now.AddDays(14));
        Assert.Equal(PolarSaleStatus.Pending, sale.ComputeStatus(now));
    }

    [Fact]
    public void Active_when_now_is_between_StartsAt_and_EndsAt()
    {
        var now = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var sale = Make(now.AddDays(-7), now.AddDays(7));
        Assert.Equal(PolarSaleStatus.Active, sale.ComputeStatus(now));
    }

    [Fact]
    public void Ended_when_EndsAt_is_in_the_past()
    {
        var now = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var sale = Make(now.AddDays(-30), now.AddDays(-7));
        Assert.Equal(PolarSaleStatus.Ended, sale.ComputeStatus(now));
    }

    [Fact]
    public void Active_when_no_bounds_set()
    {
        var sale = Make(startsAt: null, endsAt: null);
        Assert.Equal(PolarSaleStatus.Active, sale.ComputeStatus(DateTimeOffset.UtcNow));
    }
}
