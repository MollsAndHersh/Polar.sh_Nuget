namespace PolarSharp.DataSeeding;

/// <summary>
/// Top-level orchestrator for sandbox / QA / demo data generation.
/// </summary>
/// <remarks>
/// <para>
/// Composes the per-entity <c>IFakeGenerator&lt;T&gt;</c>s plus the configured
/// <see cref="ISeedSink"/>. All records produced carry <c>IsFakeData = true</c> so the
/// tenant-level <c>AllowFakeData</c> toggle from
/// <c>PolarSharp.EcommerceStoreManagement</c>'s
/// <c>TenantBusinessProfile</c> hides them from every query when set off.
/// </para>
/// <para>
/// <strong>Deterministic seeds.</strong> Every method accepts an optional <c>randomSeed</c>;
/// when supplied, running the same seed value against the same tenant produces byte-for-byte
/// identical output. Useful for CI fixtures and reproducible test data.
/// </para>
/// </remarks>
public interface IPolarDataSeeder
{
    /// <summary>Seeds <paramref name="count"/> products into <paramref name="tenantId"/>'s catalog.</summary>
    Task<SeedReport> SeedProductsAsync(string tenantId, int count, int? randomSeed = null, CancellationToken ct = default);

    /// <summary>Seeds <paramref name="count"/> categories.</summary>
    Task<SeedReport> SeedCategoriesAsync(string tenantId, int count, int? randomSeed = null, CancellationToken ct = default);

    /// <summary>Seeds <paramref name="count"/> departments.</summary>
    Task<SeedReport> SeedDepartmentsAsync(string tenantId, int count, int? randomSeed = null, CancellationToken ct = default);

    /// <summary>Seeds <paramref name="countPerKind"/> license-keys benefits.</summary>
    Task<SeedReport> SeedBenefitsAsync(string tenantId, int countPerKind, int? randomSeed = null, CancellationToken ct = default);

    /// <summary>Seeds <paramref name="count"/> discounts (mixed automatic + coupon-code).</summary>
    Task<SeedReport> SeedDiscountsAsync(string tenantId, int count, int? randomSeed = null, CancellationToken ct = default);

    /// <summary>Seeds <paramref name="count"/> checkout links.</summary>
    Task<SeedReport> SeedCheckoutLinksAsync(string tenantId, int count, int? randomSeed = null, CancellationToken ct = default);

    /// <summary>Composes every per-entity seed at a documented <see cref="SeedScale"/> volume.</summary>
    Task<SeedReport> SeedFullCatalogAsync(string tenantId, SeedScale scale, int? randomSeed = null, CancellationToken ct = default);

    /// <summary>Deletes every record where <c>IsFakeData = true</c> for the tenant.</summary>
    /// <remarks>When <paramref name="alsoArchiveInPolar"/> is true, the orchestrator additionally archives the corresponding Polar entities — useful when previous seed runs were published.</remarks>
    Task<CleanupReport> RemoveAllFakeDataAsync(string tenantId, bool alsoArchiveInPolar = false, CancellationToken ct = default);
}
