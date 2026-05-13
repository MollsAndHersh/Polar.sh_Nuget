using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Default <see cref="ILicenseKeyValidator"/> implementation. Caches successful validations
/// in <see cref="IMemoryCache"/> for a short TTL to avoid hammering Polar on every request,
/// and applies host-configured grace-period semantics on top of Polar's raw expiry.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Cache key</strong> is the SHA256 hash of the license key (NOT the key itself —
/// we never store plaintext keys, even in process memory longer than necessary). Hash
/// inputs include the current tenant id to prevent any chance of cross-tenant cache hits.
/// </para>
/// <para>
/// <strong>Grace period</strong>: when Polar reports the key as expired but
/// <c>ExpiresAt + GracePeriodDays</c> is still in the future, the validator returns
/// <see cref="LicenseValidationResult.IsValid"/> = <see langword="true"/> AND
/// <see cref="LicenseValidationResult.IsWithinGracePeriod"/> = <see langword="true"/>. The
/// host can show a "license expired — please renew" banner without revoking access.
/// </para>
/// <para>
/// <strong>Polar HTTP wiring</strong> (<see cref="IPolarLicenseKeysApi"/>) ships as a
/// best-effort wrapper in v1.3.0; sandbox validation is tracked under TASK-V20-003.
/// </para>
/// </remarks>
internal sealed class LicenseKeyValidator(
    IPolarLicenseKeysApi polarApi,
    IMultiTenantContextAccessor tenantAccessor,
    IMemoryCache cache,
    IOptionsMonitor<LicenseValidatorOptions> options,
    TimeProvider time,
    ILogger<LicenseKeyValidator> logger) : ILicenseKeyValidator
{
    private const string CacheKeyPrefix = "polar-license-key-validation:";

    private readonly IPolarLicenseKeysApi _polarApi = polarApi ?? throw new ArgumentNullException(nameof(polarApi));
    private readonly IMultiTenantContextAccessor _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly IOptionsMonitor<LicenseValidatorOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly ILogger<LicenseKeyValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<LicenseValidationResult, LicenseValidationError>> ValidateAsync(
        string licenseKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return Result<LicenseValidationResult, LicenseValidationError>.Failure(new LicenseValidationError(
                LicenseValidationErrorKind.MalformedKey, "License key must not be empty."));
        }

        if (ResolveCurrentTenant() is not { } tenant)
        {
            return Result<LicenseValidationResult, LicenseValidationError>.Failure(new LicenseValidationError(
                LicenseValidationErrorKind.PolarApiFailure,
                "Cannot validate license key — no tenant in scope."));
        }

        if (string.IsNullOrEmpty(tenant.PolarOrganizationId))
        {
            return Result<LicenseValidationResult, LicenseValidationError>.Failure(new LicenseValidationError(
                LicenseValidationErrorKind.PolarApiFailure,
                "Cannot validate license key — the current tenant has no Polar organization id (not onboarded yet)."));
        }

        // Cache key binds (tenantId, organizationId, keyHash) so no cross-tenant or cross-org collision is possible.
        var cacheKey = BuildCacheKey(tenant.Id ?? "", tenant.PolarOrganizationId, licenseKey);
        if (_cache.TryGetValue<LicenseValidationResult>(cacheKey, out var cached) && cached is not null)
        {
            _logger.LogDebug("License-key validation cache hit for tenant {TenantId}.", tenant.Id);
            return Result<LicenseValidationResult, LicenseValidationError>.Success(cached);
        }

        var apiResult = await _polarApi.ValidateAsync(
            new LicenseKeyApiRequest(licenseKey, tenant.PolarOrganizationId),
            ct).ConfigureAwait(false);

        return apiResult.Match(
            onSuccess: response => HandleApiSuccess(response, cacheKey),
            onFailure: MapApiError);
    }

    private PolarTenantInfo? ResolveCurrentTenant()
    {
        var ctx = _tenantAccessor.MultiTenantContext;
        if (ctx?.TenantInfo is PolarTenantInfo polar) return polar;
        return null;
    }

    private Result<LicenseValidationResult, LicenseValidationError> HandleApiSuccess(LicenseKeyApiResponse response, string cacheKey)
    {
        var opts = _options.CurrentValue;
        var now = _time.GetUtcNow();
        var (isValid, isWithinGrace, invalidReason) = EvaluateValidity(response, opts, now);

        var result = new LicenseValidationResult
        {
            IsValid = isValid,
            LicenseKeyId = response.LicenseKeyId,
            CustomerId = response.CustomerId,
            ExpiresAt = response.ExpiresAt,
            ActivationsRemaining = response.ActivationsRemaining,
            IsWithinGracePeriod = isWithinGrace,
            InvalidReason = invalidReason,
        };

        if (opts.CacheTtlSeconds > 0)
        {
            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(opts.CacheTtlSeconds));
        }

        return Result<LicenseValidationResult, LicenseValidationError>.Success(result);
    }

    private static (bool IsValid, bool IsWithinGrace, string? InvalidReason) EvaluateValidity(
        LicenseKeyApiResponse response,
        LicenseValidatorOptions opts,
        DateTimeOffset now)
    {
        if (!response.IsActiveAtPolar)
            return (false, false, "Polar reports the key as inactive or disabled.");

        if (response.ActivationsRemaining is 0)
            return (false, false, "All activations for this key have been used.");

        if (response.ExpiresAt is not { } expires)
            return (true, false, null);              // non-expiring key

        if (expires >= now)
            return (true, false, null);              // not yet expired

        // Past expiry — is it within the grace window?
        var graceUntil = expires.AddDays(opts.GracePeriodDays);
        if (graceUntil >= now)
            return (true, true, null);               // within grace

        return (false, false, $"Key expired on {expires:yyyy-MM-dd}.");
    }

    private static string BuildCacheKey(string tenantId, string organizationId, string licenseKey)
    {
        // Hash the license key so plaintext never lands as a dictionary key. Adding tenantId +
        // organizationId scopes the entry to the right tenant + Polar org so cache hits can
        // never cross those boundaries.
        var combined = $"{tenantId}|{organizationId}|{licenseKey}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return CacheKeyPrefix + Convert.ToHexString(hash);
    }

    private static Result<LicenseValidationResult, LicenseValidationError> MapApiError(LicenseKeyApiError err) =>
        Result<LicenseValidationResult, LicenseValidationError>.Failure(err.Kind switch
        {
            LicenseKeyApiErrorKind.MalformedKey => new LicenseValidationError(LicenseValidationErrorKind.MalformedKey, err.Message),
            LicenseKeyApiErrorKind.NotFound => new LicenseValidationError(LicenseValidationErrorKind.NotFound, err.Message),
            _ => new LicenseValidationError(LicenseValidationErrorKind.PolarApiFailure, err.Message),
        });
}
