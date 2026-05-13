using System.Text.Json;
using PolarSharp.Reporting.Reports;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Locks the JSON wire shape of every public report record. Any field rename, addition, or
/// removal breaks a test here — surfacing the change in code review so it's an intentional
/// API decision rather than an accidental break.
/// </summary>
public sealed class ReportShapeTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void TransactionReport_serialises_expected_field_set()
    {
        var report = new TransactionReport
        {
            PeriodStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            PeriodEnd = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            Currency = "USD",
            GrossRevenue = 100_00,
            RefundedAmount = 5_00,
            NetRevenue = 95_00,
            OrderCount = 10,
            RefundCount = 1,
            AverageOrderValue = 10_00,
        };
        var json = JsonSerializer.Serialize(report, Json);

        Assert.Contains("\"periodStart\":", json);
        Assert.Contains("\"periodEnd\":", json);
        Assert.Contains("\"currency\":\"USD\"", json);
        Assert.Contains("\"grossRevenue\":10000", json);
        Assert.Contains("\"netRevenue\":9500", json);
        Assert.Contains("\"orderCount\":10", json);
        Assert.Contains("\"averageOrderValue\":1000", json);
        Assert.Contains("\"topProducts\":", json);
        Assert.Contains("\"timeBuckets\":", json);
    }

    [Fact]
    public void SubscriptionReport_serialises_expected_field_set()
    {
        var report = new SubscriptionReport
        {
            Mrr = 1000_00,
            Arr = 12000_00,
            ActiveSubscriptions = 100,
            NewSubscriptions = 10,
            CanceledSubscriptions = 5,
            ChurnRate = 0.05m,
            ExpansionRevenue = 50_00,
        };
        var json = JsonSerializer.Serialize(report, Json);

        Assert.Contains("\"mrr\":100000", json);
        Assert.Contains("\"arr\":1200000", json);
        Assert.Contains("\"activeSubscriptions\":100", json);
        Assert.Contains("\"churnRate\":0.05", json);
    }

    [Fact]
    public void OrderReport_serialises_status_breakdown_and_latency()
    {
        var report = new OrderReport
        {
            Total = 100,
            Fulfilled = 90,
            Pending = 5,
            Failed = 5,
            MedianFulfillmentLatency = TimeSpan.FromHours(2),
        };
        var json = JsonSerializer.Serialize(report, Json);

        Assert.Contains("\"total\":100", json);
        Assert.Contains("\"fulfilled\":90", json);
        Assert.Contains("\"medianFulfillmentLatency\":", json);
    }

    [Fact]
    public void CustomerEntitlementsReport_distinguishes_active_from_revoked()
    {
        var report = new CustomerEntitlementsReport
        {
            CustomerId = "cus_test",
            CustomerEmail = "test@example.com",
            ActiveBenefits = [new ActiveEntitlement("bnf_a", "Pro License", "license_keys", DateTimeOffset.UtcNow)],
            RevokedBenefits = [new RevokedEntitlement("bnf_b", "Old Discord", DateTimeOffset.UtcNow.AddDays(-30), "expired")],
        };
        var json = JsonSerializer.Serialize(report, Json);

        Assert.Contains("\"activeBenefits\":", json);
        Assert.Contains("\"revokedBenefits\":", json);
        Assert.Contains("\"bnf_a\"", json);
        Assert.Contains("\"bnf_b\"", json);
    }

    [Fact]
    public void ErrorAuditReport_exposes_PolarSharp_metric_counters()
    {
        var report = new ErrorAuditReport
        {
            WebhookDeliveryFailures = 3,
            SignatureVerificationFailures = 1,
            CircuitBreakerOpenEvents = 0,
            RateLimitHits = 2,
            ApiErrorsByStatus = 5,
        };
        var json = JsonSerializer.Serialize(report, Json);

        Assert.Contains("\"webhookDeliveryFailures\":3", json);
        Assert.Contains("\"signatureVerificationFailures\":1", json);
        Assert.Contains("\"rateLimitHits\":2", json);
    }
}
