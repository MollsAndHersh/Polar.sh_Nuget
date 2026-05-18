using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Upgrade;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Tests.Upgrade;

/// <summary>
/// Tests for <see cref="SqliteSingleTenantUpgradeMigrator"/> — the SQLite-specific migrator
/// driving the single-tenant -> multi-tenant upgrade.
/// </summary>
/// <remarks>
/// <para>
/// SQLite is the one EF Core provider whose migrator can run as a true in-process unit test
/// without Testcontainers: the store is a file in the test's temp directory, opened by a real
/// <see cref="PolarTenantDbContext"/>. Every test gets its own
/// <see cref="TempDirectoryFixture"/> with a freshly-created <c>master_SaaS.db</c>.
/// </para>
/// <para>
/// The <see cref="SqliteMasterDatabaseLocator"/> is constructed inline per test pointing at
/// the fixture directory — there is no DI in play for these tests, so the locator is a plain
/// record-construction call rather than a mocked service.
/// </para>
/// </remarks>
public sealed class SqliteSingleTenantUpgradeMigratorTests : IAsyncLifetime
{
    private TempDirectoryFixture _temp = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectoryFixture();
        _connectionString = await _temp.CreateMasterSaasDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        _temp?.Dispose();
        return Task.CompletedTask;
    }

    // --- HasUpgradeCompletedAsync ------------------------------------------------------

    [Fact]
    public async Task HasUpgradeCompletedAsync_returns_false_on_fresh_database()
    {
        await using var ctx = NewContext();
        var sut = NewMigrator(ctx);

        var completed = await sut.HasUpgradeCompletedAsync(CancellationToken.None);

        Assert.False(completed);
    }

    [Fact]
    public async Task HasUpgradeCompletedAsync_returns_true_after_RunAsync_succeeds()
    {
        // First context drives RunAsync; close it cleanly before opening a second context to
        // re-read state, so the assertion is genuinely about persisted data, not in-memory cache.
        await using (var ctx = NewContext())
        {
            var migrator = NewMigrator(ctx);
            var result = await migrator.RunAsync(TestHelpers.NewTenant(), CancellationToken.None);
            Assert.True(result.Success, result.Message);
        }

        await using var verifyCtx = NewContext();
        var verifier = NewMigrator(verifyCtx);
        var completed = await verifier.HasUpgradeCompletedAsync(CancellationToken.None);

        Assert.True(completed);
    }

    // --- RunAsync registry branches ----------------------------------------------------

    [Fact]
    public async Task RunAsync_inserts_default_tenant_when_registry_empty()
    {
        await using var ctx = NewContext();
        var sut = NewMigrator(ctx);
        var defaultTenant = TestHelpers.NewTenant(identifier: "default", name: "Default");

        var result = await sut.RunAsync(defaultTenant, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.False(result.AlreadyComplete);

        var tenants = await ctx.Tenants.AsNoTracking().ToListAsync();
        var tenant = Assert.Single(tenants);
        Assert.Equal("default", tenant.Identifier);

        var history = await ctx.UpgradeHistory.AsNoTracking().SingleAsync();
        Assert.Equal(UpgradeKinds.SingleTenantToMultiTenant, history.UpgradeKind);
    }

    [Fact]
    public async Task RunAsync_supersedes_supplied_defaultTenant_when_registry_has_one_existing_entry()
    {
        // Pre-seed registry with one tenant whose identifier differs from the supplied default.
        var existingId = Guid.NewGuid();
        await using (var seedCtx = NewContext())
        {
            seedCtx.Tenants.Add(BuildEntity(existingId, "existing-tenant", "Existing Tenant Inc"));
            await seedCtx.SaveChangesAsync();
        }

        var log = new RecordingLogger<SqliteSingleTenantUpgradeMigrator>();
        await using var ctx = NewContext();
        var sut = NewMigrator(ctx, logger: log);

        var supplied = TestHelpers.NewTenant(identifier: "supplied-default");
        var result = await sut.RunAsync(supplied, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.False(result.AlreadyComplete);

        // Tenant count unchanged — still just the pre-seeded row.
        var tenants = await ctx.Tenants.AsNoTracking().ToListAsync();
        var only = Assert.Single(tenants);
        Assert.Equal("existing-tenant", only.Identifier);

        // Info-level log records the substitution.
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("superseded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_marks_upgrade_complete_when_registry_already_has_multiple_tenants()
    {
        await using (var seedCtx = NewContext())
        {
            seedCtx.Tenants.Add(BuildEntity(Guid.NewGuid(), "tenant-a", "Tenant A"));
            seedCtx.Tenants.Add(BuildEntity(Guid.NewGuid(), "tenant-b", "Tenant B"));
            await seedCtx.SaveChangesAsync();
        }

        var log = new RecordingLogger<SqliteSingleTenantUpgradeMigrator>();
        await using var ctx = NewContext();
        var sut = NewMigrator(ctx, logger: log);

        var result = await sut.RunAsync(TestHelpers.NewTenant(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains("invoked unnecessarily", StringComparison.OrdinalIgnoreCase));

        // Completion marker IS written so subsequent boots short-circuit.
        await using var verifyCtx = NewContext();
        Assert.True(await verifyCtx.UpgradeHistory.AsNoTracking()
            .AnyAsync(x => x.UpgradeKind == UpgradeKinds.SingleTenantToMultiTenant));
    }

    // --- Legacy data-file rename -------------------------------------------------------

    [Fact]
    public async Task RunAsync_renames_legacy_data_db_to_tenantId_db_when_present()
    {
        var tenant = TestHelpers.NewTenant();
        _temp.TouchFile("data.db", "fake-sqlite-payload");

        await using var ctx = NewContext();
        var sut = NewMigrator(ctx);
        await sut.RunAsync(tenant, CancellationToken.None);

        var expected = Path.Combine(_temp.Path, tenant.Id + ".db");
        Assert.True(File.Exists(expected), $"Expected per-tenant file at '{expected}'.");
        Assert.False(File.Exists(Path.Combine(_temp.Path, "data.db")),
            "Legacy data.db should have been renamed in place.");
    }

    [Fact]
    public async Task RunAsync_renames_legacy_app_db_to_tenantId_db_when_data_db_absent()
    {
        var tenant = TestHelpers.NewTenant();
        _temp.TouchFile("app.db", "fake-sqlite-payload");

        await using var ctx = NewContext();
        var sut = NewMigrator(ctx);
        await sut.RunAsync(tenant, CancellationToken.None);

        var expected = Path.Combine(_temp.Path, tenant.Id + ".db");
        Assert.True(File.Exists(expected));
        Assert.False(File.Exists(Path.Combine(_temp.Path, "app.db")));
    }

    [Fact]
    public async Task RunAsync_skips_legacy_data_db_rename_when_target_file_already_exists()
    {
        var tenant = TestHelpers.NewTenant();
        _temp.TouchFile("data.db", "legacy-payload");
        var targetPath = Path.Combine(_temp.Path, tenant.Id + ".db");
        File.WriteAllText(targetPath, "pre-existing-target-payload");

        await using var ctx = NewContext();
        var sut = NewMigrator(ctx);
        await sut.RunAsync(tenant, CancellationToken.None);

        // data.db is NOT renamed because the target already exists — idempotency safety.
        Assert.True(File.Exists(Path.Combine(_temp.Path, "data.db")),
            "data.db should NOT have been renamed when the per-tenant target already exists.");
        Assert.Equal("pre-existing-target-payload", File.ReadAllText(targetPath));
    }

    // --- Legacy master-file warning ----------------------------------------------------

    [Fact]
    public async Task RunAsync_warns_when_legacy_tenants_db_exists_alongside_master_SaaS_db()
    {
        // master_SaaS.db already exists (created by InitializeAsync). Add a stale __tenants.db.
        _temp.TouchFile(SqliteBuilderExtensions.LegacyTenantsFileName, "stale-legacy-platform-data");

        var log = new RecordingLogger<SqliteSingleTenantUpgradeMigrator>();
        await using var ctx = NewContext();
        var sut = NewMigrator(ctx, logger: log);

        await sut.RunAsync(TestHelpers.NewTenant(), CancellationToken.None);

        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains(SqliteBuilderExtensions.LegacyTenantsFileName, StringComparison.Ordinal) &&
            e.Message.Contains("operator action required", StringComparison.OrdinalIgnoreCase));

        // The migrator must NEVER delete the legacy file — operator decision.
        Assert.True(File.Exists(Path.Combine(_temp.Path, SqliteBuilderExtensions.LegacyTenantsFileName)));
    }

    // --- polar_upgrade_history row contents --------------------------------------------

    [Fact]
    public async Task RunAsync_writes_polar_upgrade_history_row_with_full_details()
    {
        var tenant = TestHelpers.NewTenant(identifier: "auditable");
        var before = DateTimeOffset.UtcNow;

        await using var ctx = NewContext();
        var sut = NewMigrator(ctx);
        await sut.RunAsync(tenant, CancellationToken.None);

        await using var verifyCtx = NewContext();
        var row = await verifyCtx.UpgradeHistory.AsNoTracking().SingleAsync();

        Assert.Equal(UpgradeKinds.SingleTenantToMultiTenant, row.UpgradeKind);
        Assert.Equal("system", row.ActorUserId);
        Assert.NotNull(row.Message);
        Assert.Contains("auditable", row.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(row.ResultSummaryJson);
        Assert.Contains("Actions", row.ResultSummaryJson!, StringComparison.Ordinal);
        Assert.True(row.CompletedAt >= before.AddSeconds(-1),
            "CompletedAt should be approximately the wall-clock instant the migrator ran.");
    }

    // --- SQLite-specific result shape --------------------------------------------------

    [Fact]
    public async Task RunAsync_returns_RowsStamped_zero_for_SQLite()
    {
        await using var ctx = NewContext();
        var sut = NewMigrator(ctx);

        var result = await sut.RunAsync(TestHelpers.NewTenant(), CancellationToken.None);

        // SQLite per-file isolation means no cross-tenant row-stamping is needed.
        Assert.Equal(0, result.RowsStamped);
        Assert.Empty(result.RowsStampedByEntityType);
    }

    // --- helpers ----------------------------------------------------------------------

    private PolarTenantDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PolarTenantDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        return new PolarTenantDbContext(options);
    }

    private SqliteSingleTenantUpgradeMigrator NewMigrator(
        PolarTenantDbContext ctx,
        ILogger<SqliteSingleTenantUpgradeMigrator>? logger = null)
    {
        // Wire the real DefaultTenantRegistryUpgrader through a real EfMultiTenantStore so the
        // migrator runs the actual production code path. The cache is an in-memory MemoryCache
        // because the upgrader does not exercise distributed-cache semantics.
        var cache = new MemoryPolarTenantCache(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new PolarTenantCacheOptions()));
        var store = new EfMultiTenantStore(ctx, cache);
        var upgrader = new DefaultTenantRegistryUpgrader(store);

        var locator = new SqliteMasterDatabaseLocator(
            DatabaseDirectory: _temp.Path,
            MasterDatabasePath: Path.Combine(_temp.Path, SqliteBuilderExtensions.MasterSaasFileName),
            UsingLegacyFileName: false);

        return new SqliteSingleTenantUpgradeMigrator(
            ctx,
            upgrader,
            locator,
            logger ?? new RecordingLogger<SqliteSingleTenantUpgradeMigrator>());
    }

    /// <summary>
    /// Builds a minimal valid <see cref="PolarTenantInfoEntity"/> for direct insertion via
    /// the DbContext when a test needs to pre-seed registry rows.
    /// </summary>
    private static PolarTenantInfoEntity BuildEntity(Guid id, string identifier, string name) => new()
    {
        Id = id.ToString(),
        Name = name,
        Slug = identifier,
        CreatedAt = DateTimeOffset.UtcNow,
        Identifier = identifier,
        PolarAccessToken = "polar_oat_test",
        SiteManagerEmail = "ops@example.com",
    };
}
