using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

/// <summary>
/// <see cref="IValidateOptions{TOptions}"/> implementation for
/// <see cref="SingleTenantUpgradeOptions"/>. Runs at startup (when the options instance is
/// first materialised via <c>ValidateOnStart</c>) and fails fast with a clear message when
/// the upgrade configuration is internally inconsistent.
/// </summary>
/// <remarks>
/// The validator covers configuration-only invariants. Runtime dependencies — e.g., whether
/// an <see cref="IDefaultTenantResolver"/> implementation is actually registered when
/// <see cref="DefaultTenantStrategy.HostSupplied"/> is selected — are checked by the
/// orchestrator at startup so the validator stays a pure function of the options instance.
/// </remarks>
internal sealed partial class SingleTenantUpgradeOptionsValidator : IValidateOptions<SingleTenantUpgradeOptions>
{
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    private const int SlugMaxLength = 64;

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, SingleTenantUpgradeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.MaxRunDuration <= TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(SingleTenantUpgradeOptions.MaxRunDuration)} must be greater than zero; " +
                $"got '{options.MaxRunDuration}'.");
        }

        if (options.DefaultTenantStrategy == DefaultTenantStrategy.LiteralDefault)
        {
            if (string.IsNullOrWhiteSpace(options.LiteralDefaultTenantSlug))
            {
                failures.Add(
                    $"{nameof(SingleTenantUpgradeOptions.LiteralDefaultTenantSlug)} is required when " +
                    $"{nameof(SingleTenantUpgradeOptions.DefaultTenantStrategy)} is " +
                    $"'{nameof(DefaultTenantStrategy.LiteralDefault)}'.");
            }
            else if (options.LiteralDefaultTenantSlug.Length > SlugMaxLength)
            {
                failures.Add(
                    $"{nameof(SingleTenantUpgradeOptions.LiteralDefaultTenantSlug)} must be " +
                    $"{SlugMaxLength} characters or fewer; got {options.LiteralDefaultTenantSlug.Length}.");
            }
            else if (!SlugPattern().IsMatch(options.LiteralDefaultTenantSlug))
            {
                failures.Add(
                    $"{nameof(SingleTenantUpgradeOptions.LiteralDefaultTenantSlug)} '{options.LiteralDefaultTenantSlug}' " +
                    "is not a valid slug. Slugs must be lowercase alphanumeric with hyphens between " +
                    "groups (no leading or trailing hyphens, no consecutive hyphens). Example: 'default', 'acme-corp'.");
            }

            if (string.IsNullOrWhiteSpace(options.LiteralDefaultTenantName))
            {
                failures.Add(
                    $"{nameof(SingleTenantUpgradeOptions.LiteralDefaultTenantName)} is required when " +
                    $"{nameof(SingleTenantUpgradeOptions.DefaultTenantStrategy)} is " +
                    $"'{nameof(DefaultTenantStrategy.LiteralDefault)}'.");
            }
        }

        // For HostSupplied: the IDefaultTenantResolver registration is enforced at runtime by
        // the orchestrator (SingleTenantUpgradeHostedService) — the validator cannot inspect
        // DI from here without violating the IValidateOptions contract.
        // For FirstUserOrganization: Stage A surfaces the unsupported-strategy error at run
        // time as a NotSupportedException with a clear message; the validator does not block
        // configuration so future stages can light up the strategy without an opt-in flag flip.

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
