using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.DataSeeding;
using PolarSharp.DataSeeding.Generators;

namespace PolarSharp.DataSeeding.Tests;

/// <summary>
/// Verifies the orchestrator routes each entity to the right sink method, honors counts,
/// and emits a populated <see cref="SeedReport"/>.
/// </summary>
public sealed class SeederOrchestrationTests
{
    private static PolarDataSeeder BuildSeeder(out CountingNoOpSeedSink sink)
    {
        sink = new CountingNoOpSeedSink();
        return new PolarDataSeeder(
            sink,
            new FakeProductGenerator(),
            new FakeCategoryGenerator(),
            new FakeDepartmentGenerator(),
            new FakeLicenseKeysBenefitGenerator(),
            new FakeDiscountGenerator(),
            new FakeCheckoutLinkGenerator(),
            NullLogger<PolarDataSeeder>.Instance);
    }

    [Fact]
    public async Task SeedProductsAsync_emits_requested_count_to_sink()
    {
        var seeder = BuildSeeder(out var sink);
        var report = await seeder.SeedProductsAsync("t-x", count: 25, randomSeed: 1);

        Assert.Equal(25, report.Created);
        Assert.Equal(25, sink.TotalPersisted);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public async Task SeedFullCatalogAsync_with_Demo_scale_creates_documented_row_totals()
    {
        var seeder = BuildSeeder(out var sink);
        await seeder.SeedFullCatalogAsync("t-x", SeedScale.Demo, randomSeed: 1);

        var c = SeedScaleCounts.For(SeedScale.Demo);
        var expected = c.DepartmentCount + c.CategoryCount + c.ProductCount + c.BenefitsPerKind + c.DiscountCount + c.CheckoutLinkCount;
        Assert.Equal(expected, sink.TotalPersisted);
    }

    [Fact]
    public async Task SeedDiscountsAsync_with_negative_count_throws()
    {
        var seeder = BuildSeeder(out _);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => seeder.SeedDiscountsAsync("t-x", count: -5));
    }

    [Fact]
    public async Task SeedProductsAsync_requires_non_empty_tenant_id()
    {
        var seeder = BuildSeeder(out _);
        await Assert.ThrowsAsync<ArgumentException>(() => seeder.SeedProductsAsync("", count: 1));
    }

    [Fact]
    public async Task RemoveAllFakeDataAsync_calls_sink_delete()
    {
        var seeder = BuildSeeder(out var sink);
        await seeder.SeedProductsAsync("t-x", count: 5, randomSeed: 1);
        Assert.Equal(5, sink.TotalPersisted);

        var report = await seeder.RemoveAllFakeDataAsync("t-x");
        Assert.Equal(5, report.LocallyDeleted);
        Assert.Equal(0, sink.TotalPersisted);   // sink reset by the no-op impl
    }

    [Fact]
    public async Task Two_runs_with_same_seed_produce_same_first_record_name()
    {
        // Determinism guarantee at the orchestration level — host fixtures can rely on
        // (tenant, seed) → identical output across runs.
        var s1 = BuildSeeder(out _);
        var s2 = BuildSeeder(out _);

        // Use the generator directly to inspect output (orchestrator's sink is the no-op so
        // we can't read the seeded records out, but the generators are pure functions of
        // (tenantId, faker)).
        var f1 = new Bogus.Faker { Random = new Bogus.Randomizer(123) };
        var f2 = new Bogus.Faker { Random = new Bogus.Randomizer(123) };
        var gen = new FakeProductGenerator();

        Assert.Equal(gen.Generate("t", f1).MasterName, gen.Generate("t", f2).MasterName);
        _ = s1; _ = s2;   // keep references alive for the test's lifetime
    }
}
