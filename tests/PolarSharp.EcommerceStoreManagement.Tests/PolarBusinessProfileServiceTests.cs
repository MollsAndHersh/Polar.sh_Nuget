using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using static PolarSharp.EcommerceStoreManagement.Tests.Infrastructure.ResultTestExtensions;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Tests for the v1.3.D <see cref="PolarBusinessProfileService"/>. Covers the full
/// Get / Save round-trip including local-vs-Polar field separation, banking-deep-link URL
/// shape, payout-status FSM transitions, and the Polar-validation-error path.
/// </summary>
public sealed class PolarBusinessProfileServiceTests
{
    private const string PolarOrgId = "org_polar_test";

    private static void Configure(IServiceCollection s, FakeOrganizationsApi api)
    {
        s.AddSingleton<IPolarOrganizationsApi>(api);
        s.AddSingleton<IOptionsMonitor<BusinessProfileOptions>>(new TestOptionsMonitor<BusinessProfileOptions>(new BusinessProfileOptions()));
        s.AddScoped<IPolarBusinessProfileService, PolarBusinessProfileService>();
    }

    [Fact]
    public async Task GetAsync_with_no_profile_row_returns_NotFound()
    {
        var api = FakeOrganizationsApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(initialTenantId: PolarOrgId, configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var result = await svc.GetAsync();

        Assert.Equal(BusinessProfileErrorKind.NotFound, result.ErrorOrThrow().Kind);
    }

    [Fact]
    public async Task SaveAsync_with_tenant_lacking_PolarOrganizationId_skips_Polar_push_and_persists_locally()
    {
        var api = FakeOrganizationsApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        // Deliberately do NOT call SetPolarOrganizationId.

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var profile = NewProfile(name: "Acme Corp");
        var save = await svc.SaveAsync(profile);

        Assert.True(save.IsSuccess);
        Assert.Empty(api.UpdateCalls);                              // Polar push skipped
        // Profile is queryable now.
        var get = await svc.GetAsync();
        Assert.Equal("Acme Corp", get.ValueOrThrow().Name);
    }

    [Fact]
    public async Task SaveAsync_with_PolarOrganizationId_pushes_writable_fields_to_Polar_then_persists_locally()
    {
        var api = FakeOrganizationsApi.SucceedsWith(new OrganizationApiResponse(PolarOrgId, "US", "USD", AccountId: null, PayoutAccountId: null));
        await using var ctx = await CatalogTestContext.CreateAsync(initialTenantId: PolarOrgId, configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var profile = NewProfile(name: "Acme Corp") with
        {
            Country = "US",
            DefaultPresentmentCurrency = "USD",
            TaxBehavior = DefaultTaxBehavior.Inclusive,
            ProductDescription = "Premium audio equipment",
        };
        var save = await svc.SaveAsync(profile);

        Assert.True(save.IsSuccess);
        Assert.Single(api.UpdateCalls);
        var pushedFields = api.UpdateCalls[0].request;
        Assert.Equal("US", pushedFields.Country);
        Assert.Equal("USD", pushedFields.DefaultPresentmentCurrency);
        Assert.Equal(DefaultTaxBehavior.Inclusive, pushedFields.TaxBehavior);
        Assert.Equal("Premium audio equipment", pushedFields.ProductDescription);
    }

    [Fact]
    public async Task SaveAsync_when_Polar_PATCH_rejects_returns_PolarValidation_and_does_not_persist_locally()
    {
        var api = FakeOrganizationsApi.FailsWith(new OrganizationApiError(OrganizationApiErrorKind.ValidationFailed, "Country code invalid"));
        await using var ctx = await CatalogTestContext.CreateAsync(initialTenantId: PolarOrgId, configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var profile = NewProfile(name: "Acme Corp") with { Country = "ZZ" };
        var save = await svc.SaveAsync(profile);

        Assert.Equal(BusinessProfileErrorKind.PolarValidation, save.ErrorOrThrow().Kind);
        // No local row should exist after the failed save.
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        Assert.Empty(await db.BusinessProfiles.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task BuildBankingSetupDeepLink_with_PolarOrgId_emits_URL_pointing_to_org_finance_page()
    {
        var api = FakeOrganizationsApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(initialTenantId: PolarOrgId, configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var link = svc.BuildBankingSetupDeepLink();

        Assert.Contains(PolarOrgId, link.ToString());
        Assert.Contains("/finance/", link.ToString());
    }

    [Fact]
    public async Task BuildBankingSetupDeepLink_without_PolarOrgId_emits_bare_dashboard_link()
    {
        var api = FakeOrganizationsApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var link = svc.BuildBankingSetupDeepLink();

        Assert.EndsWith("/dashboard", link.ToString());
    }

    [Fact]
    public async Task RefreshPayoutStatusAsync_with_no_PolarOrgId_returns_NotFound()
    {
        var api = FakeOrganizationsApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var result = await svc.RefreshPayoutStatusAsync();

        Assert.Equal(BusinessProfileErrorKind.NotFound, result.ErrorOrThrow().Kind);
    }

    [Fact]
    public async Task RefreshPayoutStatusAsync_with_both_account_ids_null_returns_NotStarted()
    {
        var saveApi = FakeOrganizationsApi.SucceedsWith(new OrganizationApiResponse(PolarOrgId, "US", "USD", null, null));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, saveApi));
        ctx.SetPolarOrganizationId(PolarOrgId);
        await SeedProfileAsync(ctx);

        // Now switch the API to return null for both account ids.
        saveApi.NextGetReturns(new OrganizationApiResponse(PolarOrgId, "US", "USD", AccountId: null, PayoutAccountId: null));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var result = await svc.RefreshPayoutStatusAsync();

        Assert.Equal(PayoutSetupStatus.NotStarted, result.ValueOrThrow());
    }

    [Fact]
    public async Task RefreshPayoutStatusAsync_with_account_id_only_returns_InProgress()
    {
        var api = FakeOrganizationsApi.SucceedsWith(new OrganizationApiResponse(PolarOrgId, "US", "USD", AccountId: "acc_xyz", PayoutAccountId: null));
        await using var ctx = await CatalogTestContext.CreateAsync(initialTenantId: PolarOrgId, configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);
        await SeedProfileAsync(ctx);

        api.NextGetReturns(new OrganizationApiResponse(PolarOrgId, "US", "USD", AccountId: "acc_xyz", PayoutAccountId: null));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var result = await svc.RefreshPayoutStatusAsync();

        Assert.Equal(PayoutSetupStatus.InProgress, result.ValueOrThrow());
    }

    [Fact]
    public async Task RefreshPayoutStatusAsync_with_both_account_ids_returns_Ready()
    {
        var api = FakeOrganizationsApi.SucceedsWith(new OrganizationApiResponse(PolarOrgId, "US", "USD", AccountId: "acc_xyz", PayoutAccountId: "pa_xyz"));
        await using var ctx = await CatalogTestContext.CreateAsync(initialTenantId: PolarOrgId, configureServices: s => Configure(s, api));
        ctx.SetPolarOrganizationId(PolarOrgId);
        await SeedProfileAsync(ctx);

        api.NextGetReturns(new OrganizationApiResponse(PolarOrgId, "US", "USD", AccountId: "acc_xyz", PayoutAccountId: "pa_xyz"));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var result = await svc.RefreshPayoutStatusAsync();

        Assert.Equal(PayoutSetupStatus.Ready, result.ValueOrThrow());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TenantBusinessProfile NewProfile(string name) => new()
    {
        Id = PolarOrgId,
        Name = name,
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        CreatedAt = DateTimeOffset.UtcNow,
        Country = "US",
        DefaultPresentmentCurrency = "USD",
        TaxBehavior = DefaultTaxBehavior.Location,
    };

    private static async Task SeedProfileAsync(CatalogTestContext ctx)
    {
        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPolarBusinessProfileService>();
        var save = await svc.SaveAsync(NewProfile(name: "Acme Corp"));
        if (save.IsFailure)
            throw new InvalidOperationException("Seed SaveAsync failed");
    }

    private sealed class FakeOrganizationsApi : IPolarOrganizationsApi
    {
        public List<(string orgId, OrganizationUpdateRequest request)> UpdateCalls { get; } = [];
        private Result<OrganizationApiResponse, OrganizationApiError> _updateResult;
        private Result<OrganizationApiResponse, OrganizationApiError>? _nextGetResult;

        private FakeOrganizationsApi(Result<OrganizationApiResponse, OrganizationApiError> updateResult)
        {
            _updateResult = updateResult;
        }

        public static FakeOrganizationsApi Idle() =>
            new(Result<OrganizationApiResponse, OrganizationApiError>.Success(new OrganizationApiResponse(PolarOrgId, "US", "USD", null, null)));

        public static FakeOrganizationsApi SucceedsWith(OrganizationApiResponse response) =>
            new(Result<OrganizationApiResponse, OrganizationApiError>.Success(response));

        public static FakeOrganizationsApi FailsWith(OrganizationApiError err) =>
            new(Result<OrganizationApiResponse, OrganizationApiError>.Failure(err));

        public void NextGetReturns(OrganizationApiResponse response) =>
            _nextGetResult = Result<OrganizationApiResponse, OrganizationApiError>.Success(response);

        public Task<Result<OrganizationApiResponse, OrganizationApiError>> UpdateAsync(string polarOrganizationId, OrganizationUpdateRequest request, CancellationToken ct)
        {
            UpdateCalls.Add((polarOrganizationId, request));
            return Task.FromResult(_updateResult);
        }

        public Task<Result<OrganizationApiResponse, OrganizationApiError>> GetAsync(string polarOrganizationId, CancellationToken ct) =>
            Task.FromResult(_nextGetResult ?? _updateResult);
    }

    private sealed class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue { get; } = currentValue;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
