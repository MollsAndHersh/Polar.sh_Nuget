using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Publishing;
using PolarSharp.EcommerceStoreManagement.Publishing;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using static PolarSharp.EcommerceStoreManagement.Tests.Infrastructure.ResultTestExtensions;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Tests for the v1.3.E <see cref="PolarCatalogPublisher"/>. Verifies dependency-ordered
/// plan computation, idempotency (create vs update based on persisted PolarXxxId),
/// dry-run semantics (no Polar mutation + no local state change), partial-failure resume
/// (PublishFailed marker persists; next AllDirty publish picks up where the last left off),
/// variant expansion (one local product with N variants expands the CreateCount by N), and
/// scope filtering (AllDirty / SingleProduct / AllFakeData / Everything).
/// </summary>
public sealed class PolarCatalogPublisherTests
{
    private static void Configure(IServiceCollection s, FakePublishingApi api)
    {
        s.AddSingleton<IPolarPublishingApi>(api);
        s.AddScoped<IPolarCatalogPublisher, PolarCatalogPublisher>();
    }

    [Fact]
    public async Task PreviewAsync_with_AllDirty_returns_only_unpublished_or_failed_entities()
    {
        var api = FakePublishingApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        await SeedBenefitAsync(ctx, name: "Draft benefit", status: PublishStatus.Draft);
        await SeedBenefitAsync(ctx, name: "Already published", status: PublishStatus.Published, polarId: "polar_b_existing");
        await SeedProductAsync(ctx, name: "Draft product", status: PublishStatus.Draft);

        using var scope = ctx.CreateScope();
        var pub = scope.ServiceProvider.GetRequiredService<IPolarCatalogPublisher>();
        var plan = await pub.PreviewAsync(new PublishOptions { Scope = PublishScope.AllDirty });

        var p = plan.ValueOrThrow();
        Assert.Equal(2, p.Actions.Count);                            // draft benefit + draft product
        Assert.Equal(2, p.CreateCount);
        Assert.Equal(0, p.UpdateCount);
        Assert.Contains(p.Actions, a => a is CreatePolarBenefit cb && cb.Local.Name == "Draft benefit");
        Assert.Contains(p.Actions, a => a is CreatePolarProduct cp && cp.Local.MasterName == "Draft product");
    }

    [Fact]
    public async Task Plan_decides_Update_over_Create_when_PolarXxxId_is_set()
    {
        var api = FakePublishingApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        // OutOfSync product that already has a Polar id from a prior publish — update path.
        await SeedProductAsync(ctx, name: "Existing product", status: PublishStatus.OutOfSync, polarId: "polar_p_abc");

        using var scope = ctx.CreateScope();
        var pub = scope.ServiceProvider.GetRequiredService<IPolarCatalogPublisher>();
        var plan = await pub.PreviewAsync(new PublishOptions { Scope = PublishScope.AllDirty });

        var p = plan.ValueOrThrow();
        Assert.Single(p.Actions);
        Assert.Equal(1, p.UpdateCount);
        Assert.Equal(0, p.CreateCount);
        var update = Assert.IsType<UpdatePolarProduct>(p.Actions[0]);
        Assert.Equal("polar_p_abc", update.ExistingPolarId);
    }

    [Fact]
    public async Task Plan_counts_variants_in_CreateCount_for_product_with_HasVariants_true()
    {
        var api = FakePublishingApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        var productId = await SeedProductAsync(ctx, name: "Variant product", status: PublishStatus.Draft, hasVariants: true);
        await SeedVariantsAsync(ctx, productId, count: 3);

        using var scope = ctx.CreateScope();
        var pub = scope.ServiceProvider.GetRequiredService<IPolarCatalogPublisher>();
        var plan = await pub.PreviewAsync(new PublishOptions { Scope = PublishScope.AllDirty });

        var p = plan.ValueOrThrow();
        Assert.Single(p.Actions);                                     // one planned action against the local product
        Assert.Equal(3, p.CreateCount);                               // but three Polar products will be created (one per variant)
    }

    [Fact]
    public async Task Dependency_order_walks_Benefits_then_Products_then_Discounts_then_CheckoutLinks()
    {
        var api = FakePublishingApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        await SeedBenefitAsync(ctx, name: "Benefit-1", status: PublishStatus.Draft);
        await SeedProductAsync(ctx, name: "Product-1", status: PublishStatus.Draft);
        await SeedDiscountAsync(ctx, name: "Discount-1", status: PublishStatus.Draft);
        await SeedCheckoutLinkAsync(ctx, name: "Link-1", status: PublishStatus.Draft);

        using var scope = ctx.CreateScope();
        var pub = scope.ServiceProvider.GetRequiredService<IPolarCatalogPublisher>();
        var plan = await pub.PreviewAsync(new PublishOptions { Scope = PublishScope.AllDirty });

        var p = plan.ValueOrThrow();
        Assert.Equal(4, p.Actions.Count);
        Assert.IsType<CreatePolarBenefit>(p.Actions[0]);
        Assert.IsType<CreatePolarProduct>(p.Actions[1]);
        Assert.IsType<CreatePolarDiscount>(p.Actions[2]);
        Assert.IsType<CreatePolarCheckoutLink>(p.Actions[3]);
    }

    [Fact]
    public async Task PublishAsync_dry_run_emits_DryRunSimulated_outcomes_and_persists_no_state()
    {
        var api = FakePublishingApi.AlwaysSucceedsAs("polar_should_not_appear");
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        await SeedBenefitAsync(ctx, name: "Test", status: PublishStatus.Draft);

        using var scope = ctx.CreateScope();
        var pub = scope.ServiceProvider.GetRequiredService<IPolarCatalogPublisher>();
        var report = await pub.PublishAsync(new PublishOptions { DryRun = true });

        var r = report.ValueOrThrow();
        Assert.Single(r.Outcomes);
        Assert.IsType<DryRunSimulated>(r.Outcomes[0]);
        Assert.Empty(api.CreateBenefitCalls);                         // No HTTP fired in dry-run

        // Confirm no state change persisted.
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var b = await db.Benefits.AsNoTracking().FirstAsync();
        Assert.Equal(PublishStatus.Draft, b.Status);                  // still Draft
        Assert.Null(b.PolarBenefitId);
    }

    [Fact]
    public async Task PublishAsync_successful_create_persists_PolarBenefitId_LastPublishedAt_and_Published_status()
    {
        var api = FakePublishingApi.AlwaysSucceedsAs("polar_b_new");
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        var benefitId = await SeedBenefitAsync(ctx, name: "Created", status: PublishStatus.Draft);

        using var scope = ctx.CreateScope();
        var pub = scope.ServiceProvider.GetRequiredService<IPolarCatalogPublisher>();
        var report = await pub.PublishAsync(new PublishOptions());

        var r = report.ValueOrThrow();
        Assert.Equal(1, r.SucceededCount);
        Assert.IsType<PublishedSuccessfully>(r.Outcomes[0]);

        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var b = await db.Benefits.AsNoTracking().FirstAsync(x => x.Id == benefitId);
        Assert.Equal("polar_b_new", b.PolarBenefitId);
        Assert.NotNull(b.LastPublishedAt);
        Assert.Equal(PublishStatus.Published, b.Status);
    }

    [Fact]
    public async Task PublishAsync_failure_marks_entity_PublishFailed_and_subsequent_AllDirty_picks_it_up()
    {
        var api = FakePublishingApi.AlwaysFails(new PolarPublishApiError(PolarPublishApiErrorKind.ValidationFailed, "Bad payload"));
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        var benefitId = await SeedBenefitAsync(ctx, name: "Will fail", status: PublishStatus.Draft);

        using var scope = ctx.CreateScope();
        var pub = scope.ServiceProvider.GetRequiredService<IPolarCatalogPublisher>();
        var report = await pub.PublishAsync(new PublishOptions());

        var r = report.ValueOrThrow();
        Assert.Equal(1, r.FailedCount);
        Assert.IsType<PublishFailedOutcome>(r.Outcomes[0]);

        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var b = await db.Benefits.AsNoTracking().FirstAsync(x => x.Id == benefitId);
        Assert.Equal(PublishStatus.PublishFailed, b.Status);

        // The next AllDirty plan should pick the failed benefit up again.
        var nextPlan = await pub.PreviewAsync(new PublishOptions { Scope = PublishScope.AllDirty });
        Assert.Single(nextPlan.ValueOrThrow().Actions);
    }

    [Fact]
    public async Task Scope_SingleProduct_loads_only_the_named_product_and_no_other_entity_types()
    {
        var api = FakePublishingApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        var target = await SeedProductAsync(ctx, name: "Target", status: PublishStatus.Draft);
        await SeedProductAsync(ctx, name: "Ignored", status: PublishStatus.Draft);
        await SeedBenefitAsync(ctx, name: "Ignored benefit", status: PublishStatus.Draft);

        using var scope = ctx.CreateScope();
        var pub = scope.ServiceProvider.GetRequiredService<IPolarCatalogPublisher>();
        var plan = await pub.PreviewAsync(new PublishOptions
        {
            Scope = PublishScope.SingleProduct,
            SingleProductId = new ProductId(target),
        });

        var p = plan.ValueOrThrow();
        Assert.Single(p.Actions);
        var cp = Assert.IsType<CreatePolarProduct>(p.Actions[0]);
        Assert.Equal("Target", cp.Local.MasterName);
    }

    [Fact]
    public async Task Scope_AllFakeData_loads_only_IsFakeData_true_entities()
    {
        var api = FakePublishingApi.Idle();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, api));
        await SeedBenefitAsync(ctx, name: "Real", status: PublishStatus.Draft);
        await SeedBenefitAsync(ctx, name: "Fake", status: PublishStatus.Published, polarId: "polar_b_fake", isFakeData: true);

        using var scope = ctx.CreateScope();
        var pub = scope.ServiceProvider.GetRequiredService<IPolarCatalogPublisher>();
        var plan = await pub.PreviewAsync(new PublishOptions { Scope = PublishScope.AllFakeData });

        var p = plan.ValueOrThrow();
        Assert.Single(p.Actions);                                     // only the fake one
        // It already has a Polar id so it's an UPDATE action.
        Assert.IsType<UpdatePolarBenefit>(p.Actions[0]);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedBenefitAsync(CatalogTestContext ctx, string name, PublishStatus status, string? polarId = null, bool isFakeData = false)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.Benefits.Add(new LocalBenefitEntity
        {
            Id = id,
            BenefitKind = "Custom",
            Name = name,
            Description = $"{name} description",
            PropertiesJson = "{}",
            PolarBenefitId = polarId,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            IsFakeData = isFakeData,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedProductAsync(CatalogTestContext ctx, string name, PublishStatus status, string? polarId = null, bool hasVariants = false, bool isFakeData = false)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.Products.Add(new LocalProductEntity
        {
            Id = id,
            MasterName = name,
            MasterDescription = $"{name} description",
            MasterLanguage = "en-US",
            Kind = ProductKind.Product,
            HasVariants = hasVariants,
            PriceJson = """{"Kind":3,"Currency":"USD","IsRecurring":false,"Amount":1000}""",
            AttachedBenefitsJson = "[]",
            PolarProductId = polarId,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            IsFakeData = isFakeData,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task SeedVariantsAsync(CatalogTestContext ctx, Guid productId, int count)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        for (var i = 0; i < count; i++)
        {
            db.Variants.Add(new LocalProductVariantEntity
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                AxesJson = $$"""{"size":"{{i}}"}""",
                IsActive = true,
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedDiscountAsync(CatalogTestContext ctx, string name, PublishStatus status)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        db.Discounts.Add(new LocalDiscountEntity
        {
            Id = Guid.NewGuid(),
            MasterName = name,
            Name = name,
            Kind = DiscountKind.Percentage,
            Type = "percentage",
            PercentageOff = 25m,
            DurationKind = DiscountDuration.Once,
            DurationWire = "once",
            ApplicableProductIdsJson = "[]",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCheckoutLinkAsync(CatalogTestContext ctx, string name, PublishStatus status)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        db.CheckoutLinks.Add(new LocalCheckoutLinkEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            ProductIdsJson = "[]",
            CustomFieldsJson = "[]",
            AllowDiscountCodes = true,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private sealed class FakePublishingApi : IPolarPublishingApi
    {
        public List<PolarBenefitPayload> CreateBenefitCalls { get; } = [];
        public List<PolarProductPayload> CreateProductCalls { get; } = [];
        private readonly Result<string, PolarPublishApiError> _result;

        private FakePublishingApi(Result<string, PolarPublishApiError> result) { _result = result; }

        public static FakePublishingApi Idle() =>
            new(Result<string, PolarPublishApiError>.Success(""));     // unused; tests using Idle don't call PublishAsync

        public static FakePublishingApi AlwaysSucceedsAs(string polarId) =>
            new(Result<string, PolarPublishApiError>.Success(polarId));

        public static FakePublishingApi AlwaysFails(PolarPublishApiError err) =>
            new(Result<string, PolarPublishApiError>.Failure(err));

        public Task<Result<string, PolarPublishApiError>> CreateProductAsync(PolarProductPayload payload, CancellationToken ct) { CreateProductCalls.Add(payload); return Task.FromResult(_result); }
        public Task<Result<string, PolarPublishApiError>> UpdateProductAsync(string polarId, PolarProductPayload payload, CancellationToken ct) => Task.FromResult(_result);
        public Task<Result<string, PolarPublishApiError>> CreateBenefitAsync(PolarBenefitPayload payload, CancellationToken ct) { CreateBenefitCalls.Add(payload); return Task.FromResult(_result); }
        public Task<Result<string, PolarPublishApiError>> UpdateBenefitAsync(string polarId, PolarBenefitPayload payload, CancellationToken ct) => Task.FromResult(_result);
        public Task<Result<string, PolarPublishApiError>> CreateDiscountAsync(PolarDiscountPayload payload, CancellationToken ct) => Task.FromResult(_result);
        public Task<Result<string, PolarPublishApiError>> UpdateDiscountAsync(string polarId, PolarDiscountPayload payload, CancellationToken ct) => Task.FromResult(_result);
        public Task<Result<string, PolarPublishApiError>> CreateCheckoutLinkAsync(PolarCheckoutLinkPayload payload, CancellationToken ct) => Task.FromResult(_result);
    }
}
