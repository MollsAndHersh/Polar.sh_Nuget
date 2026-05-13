using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PolarSharp;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Tests;

/// <summary>
/// Verifies CRUD operations on <see cref="EfMultiTenantStore"/> against an in-memory SQLite
/// database. Also confirms the cache layer hits and invalidations behave as documented.
/// </summary>
public sealed class EfMultiTenantStoreTests : IDisposable
{
    private readonly PolarTenantDbContext _db;
    private readonly EfMultiTenantStore _store;
    private readonly IPolarTenantCache _cache;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

    public EfMultiTenantStoreTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PolarTenantDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new PolarTenantDbContext(options);
        _db.Database.EnsureCreated();

        var cacheOptions = Options.Create(new PolarTenantCacheOptions
        {
            AbsoluteExpirationMinutes = 60,
            SlidingExpirationMinutes = 15,
        });
        _cache = new MemoryPolarTenantCache(new MemoryCache(new MemoryCacheOptions()), cacheOptions);
        _store = new EfMultiTenantStore(_db, _cache);
    }

    [Fact]
    public async Task TryAddAsync_then_TryGetAsync_round_trips_a_tenant()
    {
        var id = Guid.NewGuid().ToString();
        var info = new PolarTenantInfo
        {
            Id = id,
            Identifier = "acme",
            Name = "Acme Corp",
            PolarAccessToken = "polar_oat_acme",
            Server = PolarServer.Sandbox,
        };

        var added = await _store.TryAddAsync(info);
        Assert.True(added);

        var fetched = await _store.TryGetAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("acme", fetched!.Identifier);
        Assert.Equal("Acme Corp", fetched.Name);
        Assert.Equal(PolarServer.Sandbox, fetched.Server);
    }

    [Fact]
    public async Task TryGetByIdentifierAsync_finds_the_tenant()
    {
        var id = Guid.NewGuid().ToString();
        await _store.TryAddAsync(new PolarTenantInfo
        {
            Id = id, Identifier = "beta", PolarAccessToken = "tok", Server = PolarServer.Production
        });

        var fetched = await _store.TryGetByIdentifierAsync("beta");
        Assert.NotNull(fetched);
        Assert.Equal(id, fetched!.Id);
    }

    [Fact]
    public async Task TryUpdateAsync_changes_persist()
    {
        var id = Guid.NewGuid().ToString();
        await _store.TryAddAsync(new PolarTenantInfo
        {
            Id = id, Identifier = "gamma", PolarAccessToken = "old_token", Server = PolarServer.Sandbox
        });

        var update = new PolarTenantInfo
        {
            Id = id, Identifier = "gamma", PolarAccessToken = "new_token", Server = PolarServer.Production
        };
        var updated = await _store.TryUpdateAsync(update);
        Assert.True(updated);

        var fetched = await _store.TryGetAsync(id);
        Assert.Equal("new_token", fetched!.PolarAccessToken);
        Assert.Equal(PolarServer.Production, fetched.Server);
    }

    [Fact]
    public async Task TryRemoveAsync_removes_by_identifier()
    {
        var id = Guid.NewGuid().ToString();
        await _store.TryAddAsync(new PolarTenantInfo
        {
            Id = id, Identifier = "delta", PolarAccessToken = "tok", Server = PolarServer.Sandbox
        });

        var removed = await _store.TryRemoveAsync("delta");
        Assert.True(removed);

        var fetched = await _store.TryGetAsync(id);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task GetAllAsync_returns_all_tenants()
    {
        await _store.TryAddAsync(new PolarTenantInfo { Id = Guid.NewGuid().ToString(), Identifier = "a", PolarAccessToken = "t1" });
        await _store.TryAddAsync(new PolarTenantInfo { Id = Guid.NewGuid().ToString(), Identifier = "b", PolarAccessToken = "t2" });
        await _store.TryAddAsync(new PolarTenantInfo { Id = Guid.NewGuid().ToString(), Identifier = "c", PolarAccessToken = "t3" });

        var all = (await _store.GetAllAsync()).ToList();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Cache_hits_after_first_lookup()
    {
        var id = Guid.NewGuid().ToString();
        await _store.TryAddAsync(new PolarTenantInfo
        {
            Id = id, Identifier = "cached", PolarAccessToken = "tok"
        });

        // First read populates cache via the store's normal path
        await _store.TryGetAsync(id);

        // Second read should hit cache directly
        var cached = await _cache.TryGetByIdAsync(id);
        Assert.NotNull(cached);
        Assert.Equal("cached", cached!.Identifier);
    }

    [Fact]
    public async Task Cache_invalidated_after_update()
    {
        var id = Guid.NewGuid().ToString();
        await _store.TryAddAsync(new PolarTenantInfo
        {
            Id = id, Identifier = "victim", PolarAccessToken = "old"
        });

        await _store.TryGetAsync(id);   // populates cache
        await _store.TryUpdateAsync(new PolarTenantInfo
        {
            Id = id, Identifier = "victim", PolarAccessToken = "new"
        });

        var cached = await _cache.TryGetByIdAsync(id);
        Assert.Null(cached);   // invalidated by update
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
