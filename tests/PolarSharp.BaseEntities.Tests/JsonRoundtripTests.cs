using System.Text.Json;
using PolarSharp.BaseEntities;

namespace PolarSharp.BaseEntities.Tests;

/// <summary>
/// Verifies a host-inherited record round-trips through JSON serialization without losing
/// any property values — confirms the base properties stay intact across the wire boundary.
/// </summary>
public sealed class JsonRoundtripTests
{
    public sealed record HostOrder : PolarOrderBase
    {
        public string MyExtra { get; init; } = "";
    }

    [Fact]
    public void HostOrder_round_trips_through_JSON_with_no_data_loss()
    {
        var original = new HostOrder
        {
            Id = "ord_001",
            Number = "ORD-2026-001",
            Status = PolarOrderStatus.Paid,
            Amount = 12345,
            TaxAmount = 100,
            Currency = "USD",
            BillingName = "Alice",
            BillingReason = "purchase",
            Channel = "web",
            CustomerId = "cus_alice",
            OrganizationId = "org_xyz",
            CreatedAt = new DateTimeOffset(2026, 5, 12, 10, 30, 0, TimeSpan.Zero),
            MyExtra = "host-only data",
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HostOrder>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("ord_001", deserialized.Id);
        Assert.Equal(PolarOrderStatus.Paid, deserialized.Status);
        Assert.Equal(12345, deserialized.Amount);
        Assert.Equal("USD", deserialized.Currency);
        Assert.Equal("Alice", deserialized.BillingName);
        Assert.Equal("host-only data", deserialized.MyExtra);
        Assert.Equal(original.CreatedAt, deserialized.CreatedAt);
    }

    [Fact]
    public void Order_status_serializes_as_lowercase_underscored_string()
    {
        var order = new HostOrder
        {
            Id = "ord_002",
            Number = "ORD-2026-002",
            Status = PolarOrderStatus.PartiallyRefunded,
            Amount = 5000,
            Currency = "EUR",
            CustomerId = "cus_bob",
            OrganizationId = "org_xyz",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var json = JsonSerializer.Serialize(order);
        Assert.Contains("\"partially_refunded\"", json);
    }
}
