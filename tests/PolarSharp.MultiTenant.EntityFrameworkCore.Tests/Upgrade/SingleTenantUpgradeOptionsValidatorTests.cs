using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Tests.Upgrade;

/// <summary>
/// Pure-function tests for <see cref="SingleTenantUpgradeOptionsValidator"/>. The validator
/// is internal; reachable here because the EF Core production assembly declares
/// <c>InternalsVisibleTo("PolarSharp.MultiTenant.EntityFrameworkCore.Tests")</c>.
/// </summary>
public sealed class SingleTenantUpgradeOptionsValidatorTests
{
    // --- Default / happy-path cases -----------------------------------------------------

    [Fact]
    public void Validate_returns_Success_for_default_options()
    {
        var sut = new SingleTenantUpgradeOptionsValidator();
        var opts = new SingleTenantUpgradeOptions();

        var result = sut.Validate(name: null, opts);

        Assert.True(result.Succeeded, $"Expected success but got failures: {result.FailureMessage}");
    }

    [Fact]
    public void Validate_returns_Success_for_HostSupplied_strategy_regardless_of_LiteralDefault_fields()
    {
        var sut = new SingleTenantUpgradeOptionsValidator();
        var opts = new SingleTenantUpgradeOptions
        {
            DefaultTenantStrategy = DefaultTenantStrategy.HostSupplied,
            // Deliberately invalid LiteralDefault values — should be ignored under HostSupplied.
            LiteralDefaultTenantSlug = "INVALID Slug!",
            LiteralDefaultTenantName = "",
        };

        var result = sut.Validate(name: null, opts);

        Assert.True(result.Succeeded, $"Expected success but got failures: {result.FailureMessage}");
    }

    [Fact]
    public void Validate_returns_Success_for_FirstUserOrganization_strategy()
    {
        // FirstUserOrganization is rejected at runtime (NotSupportedException) but the
        // validator deliberately permits the configuration so future stages can light it up
        // without requiring an opt-in flag flip.
        var sut = new SingleTenantUpgradeOptionsValidator();
        var opts = new SingleTenantUpgradeOptions
        {
            DefaultTenantStrategy = DefaultTenantStrategy.FirstUserOrganization,
            LiteralDefaultTenantSlug = "anything-here-is-not-validated",
        };

        var result = sut.Validate(name: null, opts);

        Assert.True(result.Succeeded, $"Expected success but got failures: {result.FailureMessage}");
    }

    // --- MaxRunDuration -----------------------------------------------------------------

    [Fact]
    public void Validate_returns_Fail_for_zero_MaxRunDuration()
    {
        var sut = new SingleTenantUpgradeOptionsValidator();
        var opts = new SingleTenantUpgradeOptions { MaxRunDuration = TimeSpan.Zero };

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(SingleTenantUpgradeOptions.MaxRunDuration), result.FailureMessage);
        Assert.Contains("greater than zero", result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Fail_for_negative_MaxRunDuration()
    {
        var sut = new SingleTenantUpgradeOptionsValidator();
        var opts = new SingleTenantUpgradeOptions { MaxRunDuration = TimeSpan.FromSeconds(-5) };

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(SingleTenantUpgradeOptions.MaxRunDuration), result.FailureMessage);
        Assert.Contains("greater than zero", result.FailureMessage);
    }

    // --- LiteralDefaultTenantSlug — valid -----------------------------------------------

    [Fact]
    public void Validate_returns_Success_for_slug_default()
    {
        var result = ValidateWithSlug("default");
        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Success_for_slug_acme()
    {
        var result = ValidateWithSlug("acme");
        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Success_for_slug_tenant_1()
    {
        var result = ValidateWithSlug("tenant-1");
        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Success_for_slug_tenant_with_many_words()
    {
        var result = ValidateWithSlug("tenant-with-many-words");
        Assert.True(result.Succeeded, result.FailureMessage);
    }

    // --- LiteralDefaultTenantSlug — invalid ---------------------------------------------

    [Fact]
    public void Validate_returns_Fail_for_empty_slug()
    {
        var result = ValidateWithSlug("");
        Assert.False(result.Succeeded);
        Assert.Contains(nameof(SingleTenantUpgradeOptions.LiteralDefaultTenantSlug), result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Fail_for_slug_with_uppercase_letters()
    {
        const string slug = "Acme";
        var result = ValidateWithSlug(slug);
        Assert.False(result.Succeeded);
        Assert.Contains(slug, result.FailureMessage);
        Assert.Contains("slug", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_returns_Fail_for_slug_with_leading_hyphen()
    {
        const string slug = "-default";
        var result = ValidateWithSlug(slug);
        Assert.False(result.Succeeded);
        Assert.Contains(slug, result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Fail_for_slug_with_trailing_hyphen()
    {
        const string slug = "default-";
        var result = ValidateWithSlug(slug);
        Assert.False(result.Succeeded);
        Assert.Contains(slug, result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Fail_for_slug_with_special_characters()
    {
        const string slug = "default@!";
        var result = ValidateWithSlug(slug);
        Assert.False(result.Succeeded);
        Assert.Contains(slug, result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Fail_for_slug_with_consecutive_hyphens()
    {
        const string slug = "tenant--one";
        var result = ValidateWithSlug(slug);
        Assert.False(result.Succeeded);
        Assert.Contains(slug, result.FailureMessage);
    }

    [Fact]
    public void Validate_returns_Fail_for_slug_exceeding_max_length()
    {
        // 65 chars — one over the documented 64-char ceiling.
        var slug = new string('a', 65);
        var result = ValidateWithSlug(slug);
        Assert.False(result.Succeeded);
        Assert.Contains(nameof(SingleTenantUpgradeOptions.LiteralDefaultTenantSlug), result.FailureMessage);
    }

    // --- Aggregated failures ------------------------------------------------------------

    [Fact]
    public void Validate_aggregates_multiple_failures_into_a_single_Fail_call()
    {
        var sut = new SingleTenantUpgradeOptionsValidator();
        var opts = new SingleTenantUpgradeOptions
        {
            MaxRunDuration = TimeSpan.Zero,                  // failure #1
            DefaultTenantStrategy = DefaultTenantStrategy.LiteralDefault,
            LiteralDefaultTenantSlug = "BAD SLUG",          // failure #2
            LiteralDefaultTenantName = "",                  // failure #3
        };

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        // Each failure message contributes a distinct entry to result.Failures.
        Assert.NotNull(result.Failures);
        Assert.True(result.Failures!.Count() >= 2,
            $"Expected at least 2 aggregated failures, got: {string.Join(" | ", result.Failures!)}");
        // All three property names should appear somewhere in the combined message.
        Assert.Contains(nameof(SingleTenantUpgradeOptions.MaxRunDuration), result.FailureMessage);
        Assert.Contains(nameof(SingleTenantUpgradeOptions.LiteralDefaultTenantSlug), result.FailureMessage);
        Assert.Contains(nameof(SingleTenantUpgradeOptions.LiteralDefaultTenantName), result.FailureMessage);
    }

    // --- helpers ------------------------------------------------------------------------

    private static ValidateOptionsResult ValidateWithSlug(string slug)
    {
        var sut = new SingleTenantUpgradeOptionsValidator();
        var opts = new SingleTenantUpgradeOptions
        {
            DefaultTenantStrategy = DefaultTenantStrategy.LiteralDefault,
            LiteralDefaultTenantSlug = slug,
            LiteralDefaultTenantName = "Default Tenant",
        };
        return sut.Validate(name: null, opts);
    }
}
