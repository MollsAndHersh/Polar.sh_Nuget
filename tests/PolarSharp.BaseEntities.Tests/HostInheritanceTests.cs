using PolarSharp.BaseEntities;

namespace PolarSharp.BaseEntities.Tests;

/// <summary>
/// Verifies that host applications can inherit from every <c>PolarXxxBase</c> and add their
/// own properties without violating Polar's wire-format contract. This is the canonical
/// "host inheritance pattern" exercise — if these tests pass, hosts can use the bases.
/// </summary>
public sealed class HostInheritanceTests
{
    // ── Host-side records inheriting from the bases (the pattern hosts will use) ──

    public sealed record HostShopOrder : PolarOrderBase
    {
        public string MyHostInternalReference { get; init; } = "";
        public bool RequiresGiftWrapping { get; init; }
    }

    public sealed record HostShopCustomer : PolarCustomerBase
    {
        public string LoyaltyTier { get; init; } = "Bronze";
        public int LoyaltyPoints { get; init; }
    }

    public sealed record HostShopProduct : PolarProductBase
    {
        public string InternalSku { get; init; } = "";
        public decimal CostBasis { get; init; }
    }

    [Fact]
    public void Host_can_inherit_from_PolarOrderBase_and_add_extra_properties()
    {
        var order = new HostShopOrder
        {
            Id = "ord_123",
            Number = "ORD-12345",
            Status = PolarOrderStatus.Paid,
            Amount = 9999,
            Currency = "USD",
            CustomerId = "cus_abc",
            OrganizationId = "org_xyz",
            CreatedAt = DateTimeOffset.UtcNow,
            MyHostInternalReference = "INT-2026-001",
            RequiresGiftWrapping = true,
        };

        Assert.Equal("ord_123", order.Id);
        Assert.Equal(PolarOrderStatus.Paid, order.Status);
        Assert.True(order.RequiresGiftWrapping);
    }

    [Fact]
    public void Host_can_upcast_to_PolarOrderBase_for_polymorphic_handling()
    {
        var hostOrder = new HostShopOrder
        {
            Id = "ord_456",
            Number = "ORD-67890",
            Status = PolarOrderStatus.Pending,
            Amount = 4999,
            Currency = "EUR",
            CustomerId = "cus_def",
            OrganizationId = "org_xyz",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // The whole point: host's own type is-a PolarOrderBase. No mapping required.
        PolarOrderBase upcast = hostOrder;
        Assert.Equal("ord_456", upcast.Id);
        Assert.Equal(PolarOrderStatus.Pending, upcast.Status);
    }

    [Fact]
    public void With_expression_works_on_host_inherited_record()
    {
        var customer = new HostShopCustomer
        {
            Id = "cus_001",
            Email = "alice@example.com",
            OrganizationId = "org_xyz",
            CreatedAt = DateTimeOffset.UtcNow,
            LoyaltyPoints = 100,
        };

        var withMore = customer with { LoyaltyPoints = 250, LoyaltyTier = "Silver" };
        Assert.Equal(100, customer.LoyaltyPoints);                 // original immutable
        Assert.Equal(250, withMore.LoyaltyPoints);                 // copy has new value
        Assert.Equal("Silver", withMore.LoyaltyTier);
        Assert.Equal("alice@example.com", withMore.Email);          // base property preserved
    }

    [Fact]
    public void HostInheritance_satisfies_IPolarEntity_and_other_core_interfaces()
    {
        var product = new HostShopProduct
        {
            Id = "prod_123",
            Name = "Premium Widget",
            OrganizationId = "org_xyz",
            CreatedAt = DateTimeOffset.UtcNow,
            InternalSku = "WIDG-001",
        };

        IPolarEntity asEntity = product;
        IPolarTimestamped asTimestamped = product;
        IPolarMetadata asMetadata = product;
        IPolarOrganizationScoped asOrgScoped = product;

        Assert.Equal("prod_123", asEntity.Id);
        Assert.Equal("org_xyz", asOrgScoped.OrganizationId);
        Assert.Empty(asMetadata.Metadata);
        Assert.True(asTimestamped.CreatedAt > DateTimeOffset.MinValue);
    }
}
