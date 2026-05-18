using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.Tests.Lifecycle;

/// <summary>
/// Pure-function tests for <see cref="TenantStatusServiceOptionsValidator"/>. The validator
/// is internal; reachable here because the production assembly declares
/// <c>InternalsVisibleTo("PolarSharp.MultiTenant.Tests")</c>.
/// </summary>
public sealed class TenantStatusServiceOptionsValidatorTests
{
    // --- Default / happy-path -----------------------------------------------------------

    [Fact]
    public void Validate_returns_Success_for_default_options()
    {
        var sut = new TenantStatusServiceOptionsValidator();
        var opts = new TenantStatusServiceOptions();

        // Sanity-check the defaults documented on the options type.
        Assert.True(opts.RequireVerifiedEmailForSuspension);
        Assert.False(opts.SuspendUnverifiedTenantsAnyway);
        Assert.Equal(90, opts.DeletedTenantRetentionDays);

        var result = sut.Validate(name: null, opts);

        Assert.True(result.Succeeded, $"Expected success but got failures: {result.FailureMessage}");
    }

    // --- DeletedTenantRetentionDays — invalid -------------------------------------------

    [Fact]
    public void Validate_returns_Fail_for_zero_DeletedTenantRetentionDays()
    {
        var sut = new TenantStatusServiceOptionsValidator();
        var opts = new TenantStatusServiceOptions { DeletedTenantRetentionDays = 0 };

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(TenantStatusServiceOptions.DeletedTenantRetentionDays), result.FailureMessage);
        Assert.Contains("greater than zero", result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Fail_for_negative_DeletedTenantRetentionDays()
    {
        var sut = new TenantStatusServiceOptionsValidator();
        var opts = new TenantStatusServiceOptions { DeletedTenantRetentionDays = -7 };

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(TenantStatusServiceOptions.DeletedTenantRetentionDays), result.FailureMessage);
        Assert.Contains("greater than zero", result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Fail_for_excessively_large_DeletedTenantRetentionDays()
    {
        // One day past the documented 3650-day (~10y) sanity ceiling.
        var sut = new TenantStatusServiceOptionsValidator();
        var opts = new TenantStatusServiceOptions
        {
            DeletedTenantRetentionDays = TenantStatusServiceOptionsValidator.RetentionDaysMax + 1,
        };

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(TenantStatusServiceOptions.DeletedTenantRetentionDays), result.FailureMessage);
    }

    // --- Flag-combination matrix --------------------------------------------------------

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Validate_returns_Success_for_all_RequireVerified_and_SuspendAnyway_combinations(
        bool requireVerified,
        bool suspendAnyway)
    {
        // All four boolean combinations are valid configuration — the
        // SuspendUnverifiedTenantsAnyway override is honoured at runtime even when both
        // flags are true, so the validator does NOT reject any combination.
        var sut = new TenantStatusServiceOptionsValidator();
        var opts = new TenantStatusServiceOptions
        {
            RequireVerifiedEmailForSuspension = requireVerified,
            SuspendUnverifiedTenantsAnyway = suspendAnyway,
        };

        var result = sut.Validate(name: null, opts);

        Assert.True(result.Succeeded, $"Expected success but got failures: {result.FailureMessage}");
    }

    // --- Implements IValidateOptions ----------------------------------------------------

    [Fact]
    public void Validator_implements_IValidateOptions_for_TenantStatusServiceOptions()
    {
        Assert.IsAssignableFrom<IValidateOptions<TenantStatusServiceOptions>>(
            new TenantStatusServiceOptionsValidator());
    }
}
