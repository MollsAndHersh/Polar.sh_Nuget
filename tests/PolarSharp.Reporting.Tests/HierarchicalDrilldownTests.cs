using PolarSharp.Reporting.Drilldown;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Verifies the public shapes of the hierarchical drilldown contract — what the host's
/// hierarchical-grid UI depends on. Wire-shape regressions break consumer code, so they're
/// locked here.
/// </summary>
public sealed class HierarchicalDrilldownTests
{
    [Fact]
    public void PagedResult_HasMore_is_true_when_more_pages_remain()
    {
        var page = new PagedResult<CustomerListRow>
        {
            Rows = new List<CustomerListRow>(),
            TotalCount = 250,
            Page = 0,
            PageSize = 50,
        };
        Assert.True(page.HasMore);
    }

    [Fact]
    public void PagedResult_HasMore_is_false_on_last_page()
    {
        var page = new PagedResult<CustomerListRow>
        {
            Rows = new List<CustomerListRow>(),
            TotalCount = 250,
            Page = 4,
            PageSize = 50,
        };
        Assert.False(page.HasMore);
    }

    [Fact]
    public void DrilldownQueryBase_defaults_are_documented_safe_values()
    {
        var query = new CustomerListRequest();
        Assert.Equal(0, query.Page);
        Assert.Equal(50, query.PageSize);
        Assert.True(query.SortDescending);
        Assert.Null(query.SortBy);
    }

    [Fact]
    public void CustomerListRow_pre_aggregated_columns_match_drilldown_contract()
    {
        // The drilldown UI depends on these columns being present on the top-level row so it
        // can render order-count badges + lifetime-value totals without a per-row roll-up.
        var row = new CustomerListRow
        {
            CustomerId = "cus_test",
            Email = "test@example.com",
            OrderCount = 12,
            LifetimeValue = 45_00,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Assert.Equal(12, row.OrderCount);
        Assert.Equal(45_00, row.LifetimeValue);
    }

    [Fact]
    public void OrderSummaryRow_includes_line_item_count_and_refunded_amount()
    {
        var row = new OrderSummaryRow
        {
            OrderId = "ord_x",
            OrderNumber = "ORD-001",
            Status = "paid",
            Amount = 100_00,
            TaxAmount = 8_00,
            RefundedAmount = 10_00,
            Currency = "USD",
            LineItemCount = 3,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Assert.Equal(3, row.LineItemCount);
        Assert.Equal(10_00, row.RefundedAmount);
    }

    [Fact]
    public void OrderDrilldownDetail_aggregates_line_items_refunds_and_benefit_grants()
    {
        var detail = new OrderDrilldownDetail
        {
            OrderId = "ord_x",
            OrderNumber = "ORD-001",
            CustomerId = "cus_test",
            CustomerEmail = "test@example.com",
            Status = "paid",
            Amount = 100_00,
            TaxAmount = 8_00,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            LineItems = [
                new OrderLineItemRow("prod_a", "Product A", "price_a", 1, 50_00, 50_00, 0, 4_00),
                new OrderLineItemRow("prod_b", "Product B", "price_b", 1, 50_00, 50_00, 0, 4_00),
            ],
            Refunds = [
                new OrderRefundRow("ref_1", 10_00, "USD", "customer_request", DateTimeOffset.UtcNow),
            ],
            BenefitGrants = [
                new BenefitGrantRow("bnf_a", "License Key", "license_keys", true, DateTimeOffset.UtcNow, null),
            ],
        };

        Assert.Equal(2, detail.LineItems.Count);
        Assert.Single(detail.Refunds);
        Assert.Single(detail.BenefitGrants);
    }
}
