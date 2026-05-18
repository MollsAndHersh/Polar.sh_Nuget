using Finbuckle.MultiTenant.Abstractions;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Tests.Upgrade;

/// <summary>
/// Tests for <see cref="DefaultTenantRegistryUpgrader"/>. Uses a hand-rolled in-memory
/// <see cref="IMultiTenantStore{TTenantInfo}"/> stub so the assertions stay focused on the
/// upgrader's behaviour rather than Finbuckle's store impl.
/// </summary>
public sealed class DefaultTenantRegistryUpgraderTests
{
    // --- TenantExistsAsync --------------------------------------------------------------

    [Fact]
    public async Task TenantExistsAsync_returns_false_when_slug_not_in_store()
    {
        var store = new RecordingStore();
        var sut = new DefaultTenantRegistryUpgrader(store);

        var exists = await sut.TenantExistsAsync("missing-slug", CancellationToken.None);

        Assert.False(exists);
    }

    [Fact]
    public async Task TenantExistsAsync_returns_true_when_slug_in_store()
    {
        var store = new RecordingStore();
        store.Seed(new PolarTenantInfo
        {
            Id = Guid.NewGuid().ToString(),
            Identifier = "present",
            Name = "Present Tenant",
        });
        var sut = new DefaultTenantRegistryUpgrader(store);

        var exists = await sut.TenantExistsAsync("present", CancellationToken.None);

        Assert.True(exists);
    }

    // --- UpsertAsync — insert path ------------------------------------------------------

    [Fact]
    public async Task UpsertAsync_inserts_when_slug_not_present()
    {
        var store = new RecordingStore();
        var sut = new DefaultTenantRegistryUpgrader(store);
        var tenant = new PolarTenantInfo
        {
            Id = Guid.NewGuid().ToString(),
            Identifier = "fresh",
            Name = "Fresh Tenant",
        };

        var returned = await sut.UpsertAsync(tenant, CancellationToken.None);

        Assert.Same(tenant, returned);

        // Verify queryable afterwards via the same store.
        var roundTrip = await store.GetByIdentifierAsync("fresh");
        Assert.NotNull(roundTrip);
        Assert.Equal(tenant.Id, roundTrip!.Id);
    }

    // --- UpsertAsync — already-present path --------------------------------------------

    [Fact]
    public async Task UpsertAsync_returns_existing_tenant_when_slug_already_present()
    {
        var existingId = Guid.NewGuid().ToString();
        var existing = new PolarTenantInfo
        {
            Id = existingId,
            Identifier = "shared",
            Name = "Existing Tenant",
        };
        var store = new RecordingStore();
        store.Seed(existing);
        var sut = new DefaultTenantRegistryUpgrader(store);

        var incoming = new PolarTenantInfo
        {
            Id = Guid.NewGuid().ToString(),     // DIFFERENT id
            Identifier = "shared",              // SAME slug
            Name = "Incoming Tenant",
        };

        var returned = await sut.UpsertAsync(incoming, CancellationToken.None);

        // Returned tenant should be the existing row, not the incoming one.
        Assert.Equal(existingId, returned.Id);
        Assert.Equal("Existing Tenant", returned.Name);

        // Store still has exactly one entry for this slug — no duplicate inserted.
        Assert.Equal(1, store.Count);
        Assert.Equal(0, store.AddCalls);
    }

    // --- UpsertAsync — argument validation ---------------------------------------------

    [Fact]
    public async Task UpsertAsync_throws_ArgumentNullException_when_tenant_is_null()
    {
        var sut = new DefaultTenantRegistryUpgrader(new RecordingStore());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sut.UpsertAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task UpsertAsync_throws_ArgumentException_when_tenant_Id_is_empty()
    {
        var sut = new DefaultTenantRegistryUpgrader(new RecordingStore());
        var tenant = new PolarTenantInfo { Id = "", Identifier = "slug" };
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.UpsertAsync(tenant, CancellationToken.None));
        Assert.Contains("Id", ex.Message);
    }

    [Fact]
    public async Task UpsertAsync_throws_ArgumentException_when_tenant_Identifier_is_empty()
    {
        var sut = new DefaultTenantRegistryUpgrader(new RecordingStore());
        var tenant = new PolarTenantInfo { Id = Guid.NewGuid().ToString(), Identifier = "" };
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.UpsertAsync(tenant, CancellationToken.None));
        Assert.Contains("Identifier", ex.Message);
    }

    // --- UpsertAsync — case-sensitivity of slug matching --------------------------------

    [Fact]
    public async Task UpsertAsync_uses_case_sensitive_slug_matching_per_store_default()
    {
        // The hand-rolled stub matches identifiers via ordinal string comparison (case-sensitive).
        // That matches Finbuckle's default in-memory store behaviour: identifier matches are
        // ordinal. Production deployments using EfMultiTenantStore inherit the collation of the
        // underlying database column, which is typically case-insensitive on SQL Server with the
        // default CI_AS collation but case-sensitive on SQLite. The upgrader does not normalise
        // — it forwards the slug to the store as-is. Validator-enforced lower-case slugs make
        // the question moot in practice. This test documents the by-design pass-through.
        var existing = new PolarTenantInfo
        {
            Id = Guid.NewGuid().ToString(),
            Identifier = "tenant",
        };
        var store = new RecordingStore();
        store.Seed(existing);
        var sut = new DefaultTenantRegistryUpgrader(store);

        // Different case — store treats as not-a-match, so this becomes an insert.
        var incoming = new PolarTenantInfo
        {
            Id = Guid.NewGuid().ToString(),
            Identifier = "TENANT",
        };

        var returned = await sut.UpsertAsync(incoming, CancellationToken.None);

        Assert.Same(incoming, returned);
        Assert.Equal(2, store.Count);
    }

    // --- In-memory store stub -----------------------------------------------------------

    /// <summary>
    /// Minimal in-memory <see cref="IMultiTenantStore{TTenantInfo}"/> for unit tests.
    /// Records call counts so tests can assert the upgrader did not double-insert.
    /// Slug matching is ordinal (case-sensitive) — matches Finbuckle's documented default
    /// for non-EF stores.
    /// </summary>
    private sealed class RecordingStore : IMultiTenantStore<PolarTenantInfo>
    {
        private readonly List<PolarTenantInfo> _tenants = new();
        public int AddCalls { get; private set; }
        public int Count => _tenants.Count;

        public void Seed(PolarTenantInfo tenant) => _tenants.Add(tenant);

        public Task<bool> TryAddAsync(PolarTenantInfo tenantInfo) => AddAsync(tenantInfo);
        public Task<bool> AddAsync(PolarTenantInfo tenantInfo)
        {
            AddCalls++;
            if (_tenants.Any(t => string.Equals(t.Identifier, tenantInfo.Identifier, StringComparison.Ordinal)))
            {
                return Task.FromResult(false);
            }
            _tenants.Add(tenantInfo);
            return Task.FromResult(true);
        }

        public Task<bool> TryUpdateAsync(PolarTenantInfo tenantInfo) => UpdateAsync(tenantInfo);
        public Task<bool> UpdateAsync(PolarTenantInfo tenantInfo)
        {
            var i = _tenants.FindIndex(t => string.Equals(t.Id, tenantInfo.Id, StringComparison.Ordinal));
            if (i < 0) return Task.FromResult(false);
            _tenants[i] = tenantInfo;
            return Task.FromResult(true);
        }

        public Task<bool> TryRemoveAsync(string identifier) => RemoveAsync(identifier);
        public Task<bool> RemoveAsync(string identifier)
        {
            var removed = _tenants.RemoveAll(t =>
                string.Equals(t.Identifier, identifier, StringComparison.Ordinal));
            return Task.FromResult(removed > 0);
        }

        public Task<PolarTenantInfo?> TryGetAsync(string id) =>
            Task.FromResult(_tenants.FirstOrDefault(t =>
                string.Equals(t.Id, id, StringComparison.Ordinal)));

        public Task<PolarTenantInfo?> GetAsync(string id) => TryGetAsync(id);

        public Task<PolarTenantInfo?> TryGetByIdentifierAsync(string identifier) =>
            Task.FromResult(_tenants.FirstOrDefault(t =>
                string.Equals(t.Identifier, identifier, StringComparison.Ordinal)));

        public Task<PolarTenantInfo?> GetByIdentifierAsync(string identifier) =>
            TryGetByIdentifierAsync(identifier);

        public Task<IEnumerable<PolarTenantInfo>> GetAllAsync() =>
            Task.FromResult<IEnumerable<PolarTenantInfo>>(_tenants.ToArray());

        public Task<IEnumerable<PolarTenantInfo>> GetAllAsync(int take, int skip) =>
            Task.FromResult<IEnumerable<PolarTenantInfo>>(_tenants.Skip(skip).Take(take).ToArray());
    }
}
