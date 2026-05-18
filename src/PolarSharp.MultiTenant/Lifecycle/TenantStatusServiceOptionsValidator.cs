using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.Lifecycle;

/// <summary>
/// <see cref="IValidateOptions{TOptions}"/> implementation for
/// <see cref="TenantStatusServiceOptions"/>. Runs at startup (when the options instance is
/// first materialised via <c>ValidateOnStart</c>) and fails fast with a clear message when
/// the lifecycle configuration is internally inconsistent.
/// </summary>
/// <remarks>
/// The validator covers configuration-only invariants. The boolean policy flags
/// (<see cref="TenantStatusServiceOptions.RequireVerifiedEmailForSuspension"/> /
/// <see cref="TenantStatusServiceOptions.SuspendUnverifiedTenantsAnyway"/>) are deliberately
/// permitted in every combination — operators may legitimately want both flags set, in
/// which case the explicit override takes effect on a per-suspension basis at runtime.
/// </remarks>
internal sealed class TenantStatusServiceOptionsValidator : IValidateOptions<TenantStatusServiceOptions>
{
    /// <summary>Upper bound for <see cref="TenantStatusServiceOptions.DeletedTenantRetentionDays"/>.</summary>
    /// <remarks>
    /// 3650 days (~10 years) — chosen as a sanity ceiling, not a regulatory requirement.
    /// Hosts with retention requirements beyond a decade should manage soft-deleted tenants
    /// via a dedicated archival workflow rather than indefinite retention in the live store.
    /// </remarks>
    internal const int RetentionDaysMax = 3650;

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, TenantStatusServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.DeletedTenantRetentionDays <= 0)
        {
            failures.Add(
                $"{nameof(TenantStatusServiceOptions.DeletedTenantRetentionDays)} must be greater than zero; " +
                $"got '{options.DeletedTenantRetentionDays}'.");
        }
        else if (options.DeletedTenantRetentionDays > RetentionDaysMax)
        {
            failures.Add(
                $"{nameof(TenantStatusServiceOptions.DeletedTenantRetentionDays)} must be {RetentionDaysMax} or fewer " +
                $"(~10 years); got '{options.DeletedTenantRetentionDays}'.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
