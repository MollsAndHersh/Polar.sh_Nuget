namespace PolarSharp.EcommerceStoreManagement.Services;

/// <summary>
/// Runtime license-key validation — thin wrapper over Polar's
/// <c>POST /v1/license-keys/{id}/validate</c> with short caching, grace-period support, and
/// structured response. Used by the host's MVC action filters or middleware to gate access
/// to license-protected features.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Cache:</strong> successful validations are cached in <c>IMemoryCache</c> briefly
/// (default 60s; configurable) to avoid hammering Polar's API on every request.
/// </para>
/// <para>
/// <strong>Grace period:</strong> when a key is expired but within the configured grace
/// window, validation returns <see cref="LicenseValidationResult.IsValid"/> = <see langword="true"/>
/// AND <see cref="LicenseValidationResult.IsWithinGracePeriod"/> = <see langword="true"/>.
/// The host can show a "license expired N days ago — please renew" banner without immediately
/// revoking access.
/// </para>
/// </remarks>
public interface ILicenseKeyValidator
{
    /// <summary>Validates the supplied license key against Polar.</summary>
    /// <param name="licenseKey">The license key value supplied by the customer.</param>
    /// <param name="ct">Cancellation.</param>
    Task<Result<LicenseValidationResult, LicenseValidationError>> ValidateAsync(
        string licenseKey,
        CancellationToken ct = default);
}

/// <summary>The result of a license-key validation.</summary>
public sealed record LicenseValidationResult
{
    /// <summary>True when the key is valid for use (including within grace period).</summary>
    public required bool IsValid { get; init; }

    /// <summary>The Polar license-key id (<c>lik_xxx</c>).</summary>
    public required string LicenseKeyId { get; init; }

    /// <summary>The customer the key was issued to. <see langword="null"/> for fraudulent / unknown keys.</summary>
    public string? CustomerId { get; init; }

    /// <summary>UTC when the key expires. <see langword="null"/> for non-expiring keys.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Remaining activations before <c>MaxActivations</c> is reached. <see langword="null"/> when unlimited.</summary>
    public int? ActivationsRemaining { get; init; }

    /// <summary>Reason the key was rejected (when <see cref="IsValid"/> is false).</summary>
    public string? InvalidReason { get; init; }

    /// <summary>True when <see cref="ExpiresAt"/> is in the past but within the host-configured grace window.</summary>
    public bool IsWithinGracePeriod { get; init; }
}

/// <summary>Recoverable license-validation failure modes.</summary>
public sealed record LicenseValidationError(LicenseValidationErrorKind Kind, string Message);

/// <summary>Discriminator for license validation errors.</summary>
public enum LicenseValidationErrorKind
{
    /// <summary>The supplied key string is malformed.</summary>
    MalformedKey,
    /// <summary>The key is unknown to Polar.</summary>
    NotFound,
    /// <summary>Polar API failure (5xx, timeout).</summary>
    PolarApiFailure,
}
