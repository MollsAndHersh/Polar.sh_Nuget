using Bogus;
using PolarSharp.DataSeeding.Generators;

namespace PolarSharp.DataSeeding.Tests;

/// <summary>
/// Verifies generators produce deterministic output when given a seeded <see cref="Faker"/>,
/// and that every generated record carries <c>IsFakeData = true</c>.
/// </summary>
public sealed class GeneratorDeterminismTests
{
    [Fact]
    public void Same_seed_produces_byte_identical_product_names()
    {
        var generator = new FakeProductGenerator();
        var f1 = new Faker { Random = new Randomizer(42) };
        var f2 = new Faker { Random = new Randomizer(42) };

        var p1 = generator.Generate("t-x", f1);
        var p2 = generator.Generate("t-x", f2);

        Assert.Equal(p1.MasterName, p2.MasterName);
        Assert.Equal(p1.MasterDescription, p2.MasterDescription);
        Assert.Equal(p1.Price.Amount, p2.Price.Amount);
        Assert.Equal(p1.Manufacturer, p2.Manufacturer);
    }

    [Fact]
    public void Different_seeds_produce_different_product_names()
    {
        var generator = new FakeProductGenerator();
        var f1 = new Faker { Random = new Randomizer(1) };
        var f2 = new Faker { Random = new Randomizer(2) };
        Assert.NotEqual(generator.Generate("t-x", f1).MasterName, generator.Generate("t-x", f2).MasterName);
    }

    [Fact]
    public void Every_generator_sets_IsFakeData_to_true()
    {
        var faker = new Faker { Random = new Randomizer(7) };
        const string tenantId = "t-test";

        Assert.True(new FakeProductGenerator().Generate(tenantId, faker).IsFakeData);
        Assert.True(new FakeCategoryGenerator().Generate(tenantId, faker).IsFakeData);
        Assert.True(new FakeDepartmentGenerator().Generate(tenantId, faker).IsFakeData);
        Assert.True(new FakeLicenseKeysBenefitGenerator().Generate(tenantId, faker).IsFakeData);
        Assert.True(new FakeDiscountGenerator().Generate(tenantId, faker).IsFakeData);
        Assert.True(new FakeCheckoutLinkGenerator().Generate(tenantId, faker).IsFakeData);
    }

    [Fact]
    public void Every_generator_propagates_the_tenant_id()
    {
        var faker = new Faker { Random = new Randomizer(9) };
        const string tenantId = "tenant-xyz";

        Assert.Equal(tenantId, new FakeProductGenerator().Generate(tenantId, faker).TenantId);
        Assert.Equal(tenantId, new FakeCategoryGenerator().Generate(tenantId, faker).TenantId);
        Assert.Equal(tenantId, new FakeDepartmentGenerator().Generate(tenantId, faker).TenantId);
        Assert.Equal(tenantId, new FakeLicenseKeysBenefitGenerator().Generate(tenantId, faker).TenantId);
        Assert.Equal(tenantId, new FakeDiscountGenerator().Generate(tenantId, faker).TenantId);
        Assert.Equal(tenantId, new FakeCheckoutLinkGenerator().Generate(tenantId, faker).TenantId);
    }

    [Fact]
    public void GenerateMany_extension_produces_the_requested_count()
    {
        var generator = new FakeProductGenerator();
        var faker = new Faker { Random = new Randomizer(11) };
        var list = generator.GenerateMany("t-x", faker, 17).ToList();
        Assert.Equal(17, list.Count);
    }

    [Fact]
    public void GenerateMany_with_zero_count_yields_no_items()
    {
        var generator = new FakeProductGenerator();
        var faker = new Faker { Random = new Randomizer(11) };
        Assert.Empty(generator.GenerateMany("t-x", faker, 0));
    }

    [Fact]
    public void GenerateMany_with_negative_count_throws()
    {
        var generator = new FakeProductGenerator();
        var faker = new Faker { Random = new Randomizer(11) };
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.GenerateMany("t-x", faker, -1).ToList());
    }
}
