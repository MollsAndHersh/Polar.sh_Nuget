using System.Diagnostics;
using Bogus;
using Microsoft.Extensions.Logging;
using PolarSharp.DataSeeding.Generators;
using PolarSharp.EcommerceStoreManagement;

namespace PolarSharp.DataSeeding;

/// <summary>Default <see cref="IPolarDataSeeder"/> impl — composes the generators with the configured <see cref="ISeedSink"/>.</summary>
public sealed class PolarDataSeeder : IPolarDataSeeder
{
    private readonly ISeedSink _sink;
    private readonly FakeProductGenerator _products;
    private readonly FakeCategoryGenerator _categories;
    private readonly FakeDepartmentGenerator _departments;
    private readonly FakeLicenseKeysBenefitGenerator _benefits;
    private readonly FakeDiscountGenerator _discounts;
    private readonly FakeCheckoutLinkGenerator _checkoutLinks;
    private readonly ILogger<PolarDataSeeder> _logger;

    /// <summary>Initializes the seeder.</summary>
    public PolarDataSeeder(
        ISeedSink sink,
        FakeProductGenerator products,
        FakeCategoryGenerator categories,
        FakeDepartmentGenerator departments,
        FakeLicenseKeysBenefitGenerator benefits,
        FakeDiscountGenerator discounts,
        FakeCheckoutLinkGenerator checkoutLinks,
        ILogger<PolarDataSeeder> logger)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(departments);
        ArgumentNullException.ThrowIfNull(benefits);
        ArgumentNullException.ThrowIfNull(discounts);
        ArgumentNullException.ThrowIfNull(checkoutLinks);
        ArgumentNullException.ThrowIfNull(logger);
        _sink = sink;
        _products = products;
        _categories = categories;
        _departments = departments;
        _benefits = benefits;
        _discounts = discounts;
        _checkoutLinks = checkoutLinks;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<SeedReport> SeedProductsAsync(string tenantId, int count, int? randomSeed = null, CancellationToken ct = default)
        => RunAsync(tenantId, count, randomSeed, async (faker, n) =>
        {
            var batch = _products.GenerateMany(tenantId, faker, n).ToList();
            await _sink.PersistProductsAsync(batch, ct).ConfigureAwait(false);
            return batch.Count;
        });

    /// <inheritdoc/>
    public Task<SeedReport> SeedCategoriesAsync(string tenantId, int count, int? randomSeed = null, CancellationToken ct = default)
        => RunAsync(tenantId, count, randomSeed, async (faker, n) =>
        {
            var batch = _categories.GenerateMany(tenantId, faker, n).ToList();
            await _sink.PersistCategoriesAsync(batch, ct).ConfigureAwait(false);
            return batch.Count;
        });

    /// <inheritdoc/>
    public Task<SeedReport> SeedDepartmentsAsync(string tenantId, int count, int? randomSeed = null, CancellationToken ct = default)
        => RunAsync(tenantId, count, randomSeed, async (faker, n) =>
        {
            var batch = _departments.GenerateMany(tenantId, faker, n).ToList();
            await _sink.PersistDepartmentsAsync(batch, ct).ConfigureAwait(false);
            return batch.Count;
        });

    /// <inheritdoc/>
    public Task<SeedReport> SeedBenefitsAsync(string tenantId, int countPerKind, int? randomSeed = null, CancellationToken ct = default)
        => RunAsync(tenantId, countPerKind, randomSeed, async (faker, n) =>
        {
            // For Phase 9 we seed only the LicenseKeysBenefit kind; hosts wanting more kinds
            // can register additional IFakeGenerator<TBenefit> and call the per-kind path.
            var batch = _benefits.GenerateMany(tenantId, faker, n).Cast<LocalBenefit>().ToList();
            await _sink.PersistBenefitsAsync(batch, ct).ConfigureAwait(false);
            return batch.Count;
        });

    /// <inheritdoc/>
    public Task<SeedReport> SeedDiscountsAsync(string tenantId, int count, int? randomSeed = null, CancellationToken ct = default)
        => RunAsync(tenantId, count, randomSeed, async (faker, n) =>
        {
            var batch = _discounts.GenerateMany(tenantId, faker, n).ToList();
            await _sink.PersistDiscountsAsync(batch, ct).ConfigureAwait(false);
            return batch.Count;
        });

    /// <inheritdoc/>
    public Task<SeedReport> SeedCheckoutLinksAsync(string tenantId, int count, int? randomSeed = null, CancellationToken ct = default)
        => RunAsync(tenantId, count, randomSeed, async (faker, n) =>
        {
            var batch = _checkoutLinks.GenerateMany(tenantId, faker, n).ToList();
            await _sink.PersistCheckoutLinksAsync(batch, ct).ConfigureAwait(false);
            return batch.Count;
        });

    /// <inheritdoc/>
    public async Task<SeedReport> SeedFullCatalogAsync(string tenantId, SeedScale scale, int? randomSeed = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        var counts = SeedScaleCounts.For(scale);
        var sw = Stopwatch.StartNew();
        var totalCreated = 0;
        var warnings = new List<string>();

        // Departments + categories first — products will reference them via category id when
        // hosts upgrade to per-product category assignment (Phase 6's M:N is already in place).
        totalCreated += (await SeedDepartmentsAsync(tenantId, counts.DepartmentCount, randomSeed, ct).ConfigureAwait(false)).Created;
        totalCreated += (await SeedCategoriesAsync(tenantId, counts.CategoryCount, randomSeed, ct).ConfigureAwait(false)).Created;
        totalCreated += (await SeedProductsAsync(tenantId, counts.ProductCount, randomSeed, ct).ConfigureAwait(false)).Created;
        totalCreated += (await SeedBenefitsAsync(tenantId, counts.BenefitsPerKind, randomSeed, ct).ConfigureAwait(false)).Created;
        totalCreated += (await SeedDiscountsAsync(tenantId, counts.DiscountCount, randomSeed, ct).ConfigureAwait(false)).Created;
        totalCreated += (await SeedCheckoutLinksAsync(tenantId, counts.CheckoutLinkCount, randomSeed, ct).ConfigureAwait(false)).Created;

        _logger.LogInformation(
            "Seeded {Total} fake catalog records for tenant {TenantId} at scale {Scale} in {Duration} ms",
            totalCreated, tenantId, scale, sw.ElapsedMilliseconds);

        return new SeedReport(totalCreated, Skipped: 0, sw.Elapsed, warnings);
    }

    /// <inheritdoc/>
    public async Task<CleanupReport> RemoveAllFakeDataAsync(string tenantId, bool alsoArchiveInPolar = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        var sw = Stopwatch.StartNew();
        var deleted = await _sink.DeleteAllFakeDataAsync(ct).ConfigureAwait(false);

        // Polar-side archive is deferred — would call IPolarCatalogPublisher.ArchiveAllAsync
        // (Phase 11 once the publisher's archive path is wired to real HTTP).
        var polarArchived = 0;
        if (alsoArchiveInPolar)
        {
            _logger.LogWarning(
                "RemoveAllFakeDataAsync(alsoArchiveInPolar=true): Polar archive not yet wired. Records cleaned locally only — call IPolarCatalogPublisher.ArchiveAllAsync separately.");
        }

        return new CleanupReport(deleted, polarArchived, sw.Elapsed);
    }

    private async Task<SeedReport> RunAsync(
        string tenantId,
        int count,
        int? randomSeed,
        Func<Faker, int, Task<int>> action)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Count must be >= 0.");

        var faker = new Faker { Random = randomSeed.HasValue ? new Randomizer(randomSeed.Value) : new Randomizer() };
        var sw = Stopwatch.StartNew();
        var created = await action(faker, count).ConfigureAwait(false);
        return new SeedReport(created, Skipped: 0, sw.Elapsed, Warnings: []);
    }
}
