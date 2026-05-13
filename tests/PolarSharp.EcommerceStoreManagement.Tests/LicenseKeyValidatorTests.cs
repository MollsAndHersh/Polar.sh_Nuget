using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using static PolarSharp.EcommerceStoreManagement.Tests.Infrastructure.ResultTestExtensions;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Tests for the v1.3.C <see cref="LicenseKeyValidator"/>. Verifies input validation,
/// expiration + grace-period logic, cache hit / miss semantics, and that the validator
/// fails gracefully when the tenant has no Polar organization id set yet.
/// </summary>
public sealed class LicenseKeyValidatorTests
{
    private const string PolarOrgId = "org_polar_test";

    private static void Configure(IServiceCollection s, FakeLicenseKeysApi api, LicenseValidatorOptions? options = null)
    {
        s.AddSingleton<IPolarLicenseKeysApi>(api);
        s.AddMemoryCache();
        s.AddSingleton<IOptionsMonitor<LicenseValidatorOptions>>(new TestOptionsMonitor<LicenseValidatorOptions>(options ?? new LicenseValidatorOptions()));
        s.AddScoped<ILicenseKeyValidator, LicenseKeyValidator>();
    }

    [Fact]
    public async Task ValidateAsync_with_empty_key_returns_MalformedKey()
    {
        var api = FakeLicenseKeysApi.Empty();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ILicenseKeyValidator>();
        var result = await validator.ValidateAsync("   ");

        Assert.Equal(LicenseValidationErrorKind.MalformedKey, result.ErrorOrThrow().Kind);
        Assert.Empty(api.ValidateCalls);
    }

    [Fact]
    public async Task ValidateAsync_with_tenant_missing_PolarOrganizationId_returns_PolarApiFailure()
    {
        var api = FakeLicenseKeysApi.Empty();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        // Deliberately do NOT call SetPolarOrganizationId.

        using var scope = ctx.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ILicenseKeyValidator>();
        var result = await validator.ValidateAsync("ABC-123-XYZ");

        Assert.Equal(LicenseValidationErrorKind.PolarApiFailure, result.ErrorOrThrow().Kind);
        Assert.Contains("no Polar organization id", result.ErrorOrThrow().Message);
        Assert.Empty(api.ValidateCalls);
    }

    [Fact]
    public async Task Active_non_expiring_key_returns_IsValid_with_no_grace_period_flag()
    {
        var api = FakeLicenseKeysApi.Returns(new LicenseKeyApiResponse("lik_1", "cus_1", ExpiresAt: null, ActivationsRemaining: 5, IsActiveAtPolar: true));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ILicenseKeyValidator>();
        var result = await validator.ValidateAsync("ABC-123-XYZ");

        var v = result.ValueOrThrow();
        Assert.True(v.IsValid);
        Assert.False(v.IsWithinGracePeriod);
        Assert.Equal("lik_1", v.LicenseKeyId);
    }

    [Fact]
    public async Task Expired_key_within_grace_period_returns_Valid_with_grace_flag()
    {
        var twoDaysAgo = DateTimeOffset.UtcNow.AddDays(-2);
        var api = FakeLicenseKeysApi.Returns(new LicenseKeyApiResponse("lik_grace", "cus_1", ExpiresAt: twoDaysAgo, ActivationsRemaining: null, IsActiveAtPolar: true));
        var options = new LicenseValidatorOptions { GracePeriodDays = 7 };
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api, options));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ILicenseKeyValidator>();
        var result = await validator.ValidateAsync("ABC-123-XYZ");

        var v = result.ValueOrThrow();
        Assert.True(v.IsValid);
        Assert.True(v.IsWithinGracePeriod);
    }

    [Fact]
    public async Task Expired_key_past_grace_period_returns_Invalid()
    {
        var oldDate = DateTimeOffset.UtcNow.AddDays(-30);
        var api = FakeLicenseKeysApi.Returns(new LicenseKeyApiResponse("lik_old", "cus_1", ExpiresAt: oldDate, ActivationsRemaining: null, IsActiveAtPolar: true));
        var options = new LicenseValidatorOptions { GracePeriodDays = 7 };
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api, options));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ILicenseKeyValidator>();
        var result = await validator.ValidateAsync("ABC-123-XYZ");

        var v = result.ValueOrThrow();
        Assert.False(v.IsValid);
        Assert.False(v.IsWithinGracePeriod);
        Assert.NotNull(v.InvalidReason);
        Assert.Contains("expired", v.InvalidReason!);
    }

    [Fact]
    public async Task Polar_reports_inactive_key_returns_Invalid()
    {
        var api = FakeLicenseKeysApi.Returns(new LicenseKeyApiResponse("lik_inactive", "cus_1", ExpiresAt: null, ActivationsRemaining: 5, IsActiveAtPolar: false));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ILicenseKeyValidator>();
        var result = await validator.ValidateAsync("ABC-123-XYZ");

        var v = result.ValueOrThrow();
        Assert.False(v.IsValid);
    }

    [Fact]
    public async Task ActivationsRemaining_zero_returns_Invalid()
    {
        var api = FakeLicenseKeysApi.Returns(new LicenseKeyApiResponse("lik_exhausted", "cus_1", ExpiresAt: null, ActivationsRemaining: 0, IsActiveAtPolar: true));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ILicenseKeyValidator>();
        var result = await validator.ValidateAsync("ABC-123-XYZ");

        Assert.False(result.ValueOrThrow().IsValid);
    }

    [Fact]
    public async Task Cache_hit_on_second_call_avoids_second_API_call()
    {
        var api = FakeLicenseKeysApi.Returns(new LicenseKeyApiResponse("lik_1", "cus_1", ExpiresAt: null, ActivationsRemaining: null, IsActiveAtPolar: true));
        var options = new LicenseValidatorOptions { CacheTtlSeconds = 60 };
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api, options));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ILicenseKeyValidator>();

        await validator.ValidateAsync("ABC-123-XYZ");
        await validator.ValidateAsync("ABC-123-XYZ");

        Assert.Single(api.ValidateCalls);
    }

    [Fact]
    public async Task Disabled_cache_calls_API_on_every_request()
    {
        var api = FakeLicenseKeysApi.Returns(new LicenseKeyApiResponse("lik_1", "cus_1", ExpiresAt: null, ActivationsRemaining: null, IsActiveAtPolar: true));
        var options = new LicenseValidatorOptions { CacheTtlSeconds = 0 };
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api, options));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ILicenseKeyValidator>();

        await validator.ValidateAsync("ABC-123-XYZ");
        await validator.ValidateAsync("ABC-123-XYZ");
        await validator.ValidateAsync("ABC-123-XYZ");

        Assert.Equal(3, api.ValidateCalls.Count);
    }

    [Fact]
    public async Task Polar_NotFound_maps_to_public_NotFound_error()
    {
        var api = FakeLicenseKeysApi.Fails(new LicenseKeyApiError(LicenseKeyApiErrorKind.NotFound, "Unknown key"));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ILicenseKeyValidator>();
        var result = await validator.ValidateAsync("ABC-123-XYZ");

        Assert.Equal(LicenseValidationErrorKind.NotFound, result.ErrorOrThrow().Kind);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class FakeLicenseKeysApi : IPolarLicenseKeysApi
    {
        public List<LicenseKeyApiRequest> ValidateCalls { get; } = [];
        private readonly Result<LicenseKeyApiResponse, LicenseKeyApiError> _result;

        private FakeLicenseKeysApi(Result<LicenseKeyApiResponse, LicenseKeyApiError> result) { _result = result; }

        public static FakeLicenseKeysApi Returns(LicenseKeyApiResponse response) =>
            new(Result<LicenseKeyApiResponse, LicenseKeyApiError>.Success(response));

        public static FakeLicenseKeysApi Fails(LicenseKeyApiError err) =>
            new(Result<LicenseKeyApiResponse, LicenseKeyApiError>.Failure(err));

        public static FakeLicenseKeysApi Empty() =>
            new(Result<LicenseKeyApiResponse, LicenseKeyApiError>.Failure(new LicenseKeyApiError(LicenseKeyApiErrorKind.UnexpectedFailure, "unused")));

        public Task<Result<LicenseKeyApiResponse, LicenseKeyApiError>> ValidateAsync(LicenseKeyApiRequest request, CancellationToken ct)
        {
            ValidateCalls.Add(request);
            return Task.FromResult(_result);
        }
    }

    private sealed class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue { get; } = currentValue;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
