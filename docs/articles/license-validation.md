# License key validation

`ILicenseKeyValidator` in `PolarSharp.EcommerceStoreManagement` is the runtime wrapper around Polar's `POST /v1/license-keys/{id}/validate` endpoint. Returns a structured `LicenseValidationResult`, caches successful validations briefly, and supports per-tenant grace periods.

## Service surface

```csharp
public interface ILicenseKeyValidator
{
    Task<Result<LicenseValidationResult, PolarError>> ValidateAsync(
        string licenseKey,
        CancellationToken ct = default);
}

public sealed record LicenseValidationResult
{
    public required bool IsValid { get; init; }
    public required string LicenseKeyId { get; init; }
    public Option<string> CustomerId { get; init; }
    public Option<DateTimeOffset> ExpiresAt { get; init; }
    public Option<int> ActivationsRemaining { get; init; }
    public Option<string> InvalidReason { get; init; }     // "expired" | "revoked" | "max_activations" | etc.
    public bool IsWithinGracePeriod { get; init; }
}
```

## Caching

Successful validations cache in `IMemoryCache` for `LicenseValidatorOptions.CacheTtlSeconds` (default 60s) to avoid hammering Polar's API when a license key is validated on every request. Set `CacheTtlSeconds=0` to disable caching for high-security scenarios.

```csharp
services.Configure<LicenseValidatorOptions>(opts =>
{
    opts.CacheTtlSeconds = 60;
    opts.GracePeriodDays = 7;
});
```

## Grace period

`GracePeriodDays` (default 7) lets the host show "Your license expired N days ago — please renew" without revoking access immediately. `LicenseValidationResult.IsWithinGracePeriod=true` means `ExpiresAt` is in the past but within the configured grace window. The host decides what to do — allow read-only, show a banner, etc.

## MVC action filter convenience

```csharp
[RequireValidLicense]
public IActionResult RestrictedFeature() => Ok();
```

Reads the `X-License-Key` header (configurable), calls `ILicenseKeyValidator`, short-circuits with HTTP 403 + structured response if invalid (unless within grace period).

## v2.0 deferral

The Polar HTTP impl behind `IPolarLicenseKeysApi` is a deferred stub (`PolarClientLicenseKeysApi`, tracked as TASK-V20-003) until the Kiota request builder for `/v1/license-keys/{id}/validate` is wired through `PolarClient`. Hosts implementing custom `IPolarLicenseKeysApi` against the live Polar API work today.
