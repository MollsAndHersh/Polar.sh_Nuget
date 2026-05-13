using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Publishing;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;
using PolarSharp.EcommerceStoreManagement.Publishing;
using PolarSharp.EcommerceStoreManagement.Reading;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Verifies the v1.3.G <c>AddPolarEcommerce</c> orchestrator registers every documented
/// EcommerceStoreManagement service in a single one-line call. Resolution is the litmus
/// test — if anything is missing, the test scope can't materialise the service.
/// </summary>
public sealed class AddPolarEcommerceTests
{
    /// <summary>
    /// Pre-registers stub Polar HTTP APIs so the orchestrator's <c>TryAddScoped</c>
    /// PolarClient-backed defaults are skipped — keeps the test scope from needing the
    /// full <c>AddPolarInfrastructure</c> wiring (PolarClient + access token + etc).
    /// </summary>
    private static void ConfigureWithStubApis(IServiceCollection s)
    {
        s.AddScoped<IPolarRefundsApi, StubRefundsApi>();
        s.AddScoped<IPolarLicenseKeysApi, StubLicenseKeysApi>();
        s.AddScoped<IPolarOrganizationsApi, StubOrganizationsApi>();
        s.AddScoped<IPolarPublishingApi, StubPublishingApi>();
        s.AddPolarEcommerce(EmptyConfiguration());
    }

    [Fact]
    public async Task AddPolarEcommerce_registers_translation_resolver_and_repo_and_reader_and_cache()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: ConfigureWithStubApis);

        using var scope = ctx.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<ITranslationProviderResolver>());
        Assert.NotNull(scope.ServiceProvider.GetService<ITenantTranslationConfigLookup>());
        Assert.NotNull(scope.ServiceProvider.GetService<ITranslationRepository>());
        Assert.NotNull(scope.ServiceProvider.GetService<IPolarCatalogReader>());
        Assert.NotNull(scope.ServiceProvider.GetService<IPolarCatalogTranslationCache>());
    }

    [Fact]
    public async Task AddPolarEcommerce_registers_refund_license_business_profile_inventory_publisher_services()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: ConfigureWithStubApis);

        using var scope = ctx.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IRefundService>());
        Assert.NotNull(scope.ServiceProvider.GetService<ILicenseKeyValidator>());
        Assert.NotNull(scope.ServiceProvider.GetService<IPolarBusinessProfileService>());
        Assert.NotNull(scope.ServiceProvider.GetService<IInventoryUpdater>());
        Assert.NotNull(scope.ServiceProvider.GetService<IPolarCatalogPublisher>());
        Assert.NotNull(scope.ServiceProvider.GetService<IAuditLogActorProvider>());
    }

    [Fact]
    public async Task AddPolarEcommerce_registers_all_five_cloning_services()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: ConfigureWithStubApis);

        using var scope = ctx.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IProductCloningService>());
        Assert.NotNull(scope.ServiceProvider.GetService<ICategoryCloningService>());
        Assert.NotNull(scope.ServiceProvider.GetService<IBenefitCloningService>());
        Assert.NotNull(scope.ServiceProvider.GetService<IDiscountCloningService>());
        Assert.NotNull(scope.ServiceProvider.GetService<ICheckoutLinkCloningService>());
    }

    [Fact]
    public async Task AddPolarEcommerce_is_idempotent_when_called_twice()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s =>
        {
            ConfigureWithStubApis(s);
            s.AddPolarEcommerce(EmptyConfiguration());      // second call must not break anything
        });

        using var scope = ctx.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IRefundService>());
    }

    private static IConfiguration EmptyConfiguration() =>
        new ConfigurationBuilder().Build();

    // Stub Polar HTTP APIs so the orchestrator doesn't try to instantiate PolarClient.
    private sealed class StubRefundsApi : IPolarRefundsApi
    {
        public Task<Result<RefundApiResponse, RefundApiError>> CreateRefundAsync(RefundApiRequest r, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<RefundApiResponse>, RefundApiError>> ListRefundsForOrderAsync(string o, CancellationToken c) => throw new NotImplementedException();
    }
    private sealed class StubLicenseKeysApi : IPolarLicenseKeysApi
    {
        public Task<Result<LicenseKeyApiResponse, LicenseKeyApiError>> ValidateAsync(LicenseKeyApiRequest r, CancellationToken c) => throw new NotImplementedException();
    }
    private sealed class StubOrganizationsApi : IPolarOrganizationsApi
    {
        public Task<Result<OrganizationApiResponse, OrganizationApiError>> UpdateAsync(string o, OrganizationUpdateRequest r, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<OrganizationApiResponse, OrganizationApiError>> GetAsync(string o, CancellationToken c) => throw new NotImplementedException();
    }
    private sealed class StubPublishingApi : IPolarPublishingApi
    {
        public Task<Result<string, PolarPublishApiError>> CreateProductAsync(PolarProductPayload p, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<string, PolarPublishApiError>> UpdateProductAsync(string id, PolarProductPayload p, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<string, PolarPublishApiError>> CreateBenefitAsync(PolarBenefitPayload p, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<string, PolarPublishApiError>> UpdateBenefitAsync(string id, PolarBenefitPayload p, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<string, PolarPublishApiError>> CreateDiscountAsync(PolarDiscountPayload p, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<string, PolarPublishApiError>> UpdateDiscountAsync(string id, PolarDiscountPayload p, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<string, PolarPublishApiError>> CreateCheckoutLinkAsync(PolarCheckoutLinkPayload p, CancellationToken c) => throw new NotImplementedException();
    }
}
