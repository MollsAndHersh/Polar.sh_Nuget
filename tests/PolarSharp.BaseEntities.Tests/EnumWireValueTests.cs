using System.Text.Json;
using PolarSharp.BaseEntities;

namespace PolarSharp.BaseEntities.Tests;

/// <summary>
/// Verifies that every enum value serializes to the EXACT wire-format string Polar emits.
/// A failure here means the host's webhook deserialization will silently misinterpret
/// statuses — critical to catch.
/// </summary>
public sealed class EnumWireValueTests
{
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

    [Theory]
    [InlineData(PolarOrderStatus.Pending,           "\"pending\"")]
    [InlineData(PolarOrderStatus.Paid,              "\"paid\"")]
    [InlineData(PolarOrderStatus.Refunded,          "\"refunded\"")]
    [InlineData(PolarOrderStatus.PartiallyRefunded, "\"partially_refunded\"")]
    [InlineData(PolarOrderStatus.Void,              "\"void\"")]
    public void OrderStatus_serializes_to_Polar_wire_string(PolarOrderStatus value, string expected)
        => Assert.Equal(expected, Serialize(value));

    [Theory]
    [InlineData(PolarSubscriptionStatus.Incomplete,        "\"incomplete\"")]
    [InlineData(PolarSubscriptionStatus.IncompleteExpired, "\"incomplete_expired\"")]
    [InlineData(PolarSubscriptionStatus.Trialing,          "\"trialing\"")]
    [InlineData(PolarSubscriptionStatus.Active,            "\"active\"")]
    [InlineData(PolarSubscriptionStatus.PastDue,           "\"past_due\"")]
    [InlineData(PolarSubscriptionStatus.Canceled,          "\"canceled\"")]
    [InlineData(PolarSubscriptionStatus.Unpaid,            "\"unpaid\"")]
    public void SubscriptionStatus_serializes_to_Polar_wire_string(PolarSubscriptionStatus value, string expected)
        => Assert.Equal(expected, Serialize(value));

    [Theory]
    [InlineData(PolarRefundStatus.Pending,   "\"pending\"")]
    [InlineData(PolarRefundStatus.Succeeded, "\"succeeded\"")]
    [InlineData(PolarRefundStatus.Failed,    "\"failed\"")]
    public void RefundStatus_serializes_to_Polar_wire_string(PolarRefundStatus value, string expected)
        => Assert.Equal(expected, Serialize(value));

    [Theory]
    [InlineData(PolarRefundReason.Duplicate,             "\"duplicate\"")]
    [InlineData(PolarRefundReason.Fraudulent,            "\"fraudulent\"")]
    [InlineData(PolarRefundReason.CustomerRequest,       "\"customer_request\"")]
    [InlineData(PolarRefundReason.ServiceDisruption,     "\"service_disruption\"")]
    [InlineData(PolarRefundReason.SatisfactionGuarantee, "\"satisfaction_guarantee\"")]
    [InlineData(PolarRefundReason.DisputePrevention,     "\"dispute_prevention\"")]
    [InlineData(PolarRefundReason.Other,                 "\"other\"")]
    public void RefundReason_serializes_to_Polar_wire_string(PolarRefundReason value, string expected)
        => Assert.Equal(expected, Serialize(value));

    [Theory]
    [InlineData(PolarCheckoutStatus.Open,      "\"open\"")]
    [InlineData(PolarCheckoutStatus.Expired,   "\"expired\"")]
    [InlineData(PolarCheckoutStatus.Confirmed, "\"confirmed\"")]
    public void CheckoutStatus_serializes_to_Polar_wire_string(PolarCheckoutStatus value, string expected)
        => Assert.Equal(expected, Serialize(value));

    [Theory]
    [InlineData(PolarLicenseKeyStatus.Active,   "\"active\"")]
    [InlineData(PolarLicenseKeyStatus.Inactive, "\"inactive\"")]
    [InlineData(PolarLicenseKeyStatus.Disabled, "\"disabled\"")]
    public void LicenseKeyStatus_serializes_to_Polar_wire_string(PolarLicenseKeyStatus value, string expected)
        => Assert.Equal(expected, Serialize(value));

    [Theory]
    [InlineData(PolarRecurringInterval.None,      "\"none\"")]
    [InlineData(PolarRecurringInterval.Weekly,    "\"weekly\"")]
    [InlineData(PolarRecurringInterval.Monthly,   "\"monthly\"")]
    [InlineData(PolarRecurringInterval.Quarterly, "\"quarterly\"")]
    [InlineData(PolarRecurringInterval.Biannual,  "\"biannual\"")]
    [InlineData(PolarRecurringInterval.Yearly,    "\"yearly\"")]
    public void RecurringInterval_serializes_to_Polar_wire_string(PolarRecurringInterval value, string expected)
        => Assert.Equal(expected, Serialize(value));

    [Theory]
    [InlineData(PolarTrialInterval.Days,   "\"days\"")]
    [InlineData(PolarTrialInterval.Weeks,  "\"weeks\"")]
    [InlineData(PolarTrialInterval.Months, "\"months\"")]
    public void TrialInterval_serializes_to_Polar_wire_string(PolarTrialInterval value, string expected)
        => Assert.Equal(expected, Serialize(value));

    [Theory]
    [InlineData(PolarOrganizationStatus.Active,        "\"active\"")]
    [InlineData(PolarOrganizationStatus.PendingReview, "\"pending_review\"")]
    [InlineData(PolarOrganizationStatus.Suspended,     "\"suspended\"")]
    public void OrganizationStatus_serializes_to_Polar_wire_string(PolarOrganizationStatus value, string expected)
        => Assert.Equal(expected, Serialize(value));

    [Theory]
    [InlineData(PolarBenefitType.Custom,           "\"custom\"")]
    [InlineData(PolarBenefitType.Discord,          "\"discord\"")]
    [InlineData(PolarBenefitType.Downloadables,    "\"downloadables\"")]
    [InlineData(PolarBenefitType.FeatureFlag,      "\"feature_flag\"")]
    [InlineData(PolarBenefitType.GithubRepository, "\"github_repository\"")]
    [InlineData(PolarBenefitType.LicenseKeys,      "\"license_keys\"")]
    [InlineData(PolarBenefitType.MeterCredit,      "\"meter_credit\"")]
    public void BenefitType_serializes_to_Polar_wire_string(PolarBenefitType value, string expected)
        => Assert.Equal(expected, Serialize(value));

    [Fact]
    public void Enums_round_trip_from_wire_string()
    {
        const string json = "\"customer_request\"";
        var deserialized = JsonSerializer.Deserialize<PolarRefundReason>(json);
        Assert.Equal(PolarRefundReason.CustomerRequest, deserialized);
    }
}
