using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Implementation of Finbuckle's <see cref="IMultiTenantStore{TTenantInfo}"/> backed by EF Core.
/// </summary>
/// <remarks>
/// <para>
/// Resolves <see cref="PolarTenantInfo"/> records on every request via the configured
/// <see cref="PolarTenantDbContext"/>. Each read passes through the <see cref="IPolarTenantCache"/>
/// layer first; cache misses fall through to the DbContext and populate the cache on success.
/// </para>
/// <para>
/// <strong>Cache invalidation:</strong> writes (<see cref="AddAsync"/>, <see cref="UpdateAsync"/>,
/// <see cref="RemoveAsync"/>) invalidate the affected tenant from the cache AFTER the EF
/// commit succeeds — so a failed DB write leaves the cache untouched.
/// </para>
/// </remarks>
public sealed class EfMultiTenantStore : IMultiTenantStore<PolarTenantInfo>
{
    private readonly PolarTenantDbContext _db;
    private readonly IPolarTenantCache _cache;

    /// <summary>Initializes a new store backed by the given DbContext and cache.</summary>
    /// <param name="db">The EF Core tenant registry context.</param>
    /// <param name="cache">The tenant cache (memory or distributed).</param>
    public EfMultiTenantStore(PolarTenantDbContext db, IPolarTenantCache cache)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        _db = db;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<PolarTenantInfo?> TryGetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var cached = await _cache.TryGetByIdAsync(id).ConfigureAwait(false);
        if (cached is not null) return cached;

        var entity = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id).ConfigureAwait(false);
        if (entity is null) return null;

        var info = ToTenantInfo(entity);
        await _cache.SetAsync(info).ConfigureAwait(false);
        return info;
    }

    /// <inheritdoc/>
    public async Task<PolarTenantInfo?> TryGetByIdentifierAsync(string identifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        var cached = await _cache.TryGetByIdentifierAsync(identifier).ConfigureAwait(false);
        if (cached is not null) return cached;

        var entity = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Identifier == identifier).ConfigureAwait(false);
        if (entity is null) return null;

        var info = ToTenantInfo(entity);
        await _cache.SetAsync(info).ConfigureAwait(false);
        return info;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PolarTenantInfo>> GetAllAsync()
    {
        var entities = await _db.Tenants.AsNoTracking().OrderBy(t => t.Id).ToListAsync().ConfigureAwait(false);
        return entities.Select(ToTenantInfo).ToList();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PolarTenantInfo>> GetAllAsync(int take, int skip)
    {
        var query = _db.Tenants.AsNoTracking().OrderBy(t => t.Id).AsQueryable();
        if (skip > 0) query = query.Skip(skip);
        if (take > 0) query = query.Take(take);
        var entities = await query.ToListAsync().ConfigureAwait(false);
        return entities.Select(ToTenantInfo).ToList();
    }

    /// <inheritdoc/>
    public Task<PolarTenantInfo?> GetAsync(string id) => TryGetAsync(id);

    /// <inheritdoc/>
    public Task<PolarTenantInfo?> GetByIdentifierAsync(string identifier) => TryGetByIdentifierAsync(identifier);

    /// <inheritdoc/>
    public async Task<bool> TryAddAsync(PolarTenantInfo tenantInfo)
    {
        ArgumentNullException.ThrowIfNull(tenantInfo);
        if (string.IsNullOrEmpty(tenantInfo.Id)) return false;

        var entity = ToEntity(tenantInfo);
        _db.Tenants.Add(entity);
        try
        {
            await _db.SaveChangesAsync().ConfigureAwait(false);
            await _cache.SetAsync(tenantInfo).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<bool> AddAsync(PolarTenantInfo tenantInfo) => TryAddAsync(tenantInfo);

    /// <inheritdoc/>
    public async Task<bool> TryUpdateAsync(PolarTenantInfo tenantInfo)
    {
        ArgumentNullException.ThrowIfNull(tenantInfo);
        if (string.IsNullOrEmpty(tenantInfo.Id)) return false;

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantInfo.Id).ConfigureAwait(false);
        if (entity is null) return false;

        // PolarTenantInfoEntity is a `record` with `init`-only properties inherited from
        // PolarTenantBase, so we build a fresh entity with the new values and let EF Core
        // copy them onto the tracked instance via SetValues (which uses internal property
        // accessors that bypass the C# init-only restriction).
        var updated = entity with
        {
            Identifier = tenantInfo.Identifier ?? entity.Identifier,
            Name = tenantInfo.Name ?? entity.Name,
            PolarAccessToken = tenantInfo.PolarAccessToken,
            Server = tenantInfo.Server,
            LifecycleStatus = tenantInfo.Status,
            SiteManagerEmail = tenantInfo.SiteManagerEmail,
            SiteManagerEmailVerified = tenantInfo.SiteManagerEmailVerified,
            SiteManagerPhone = tenantInfo.SiteManagerPhone,
        };
        _db.Entry(entity).CurrentValues.SetValues(updated);

        await _db.SaveChangesAsync().ConfigureAwait(false);
        await _cache.InvalidateAsync(tenantInfo.Id!).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public Task<bool> UpdateAsync(PolarTenantInfo tenantInfo) => TryUpdateAsync(tenantInfo);

    /// <inheritdoc/>
    public async Task<bool> TryRemoveAsync(string identifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Identifier == identifier).ConfigureAwait(false);
        if (entity is null) return false;

        _db.Tenants.Remove(entity);
        await _db.SaveChangesAsync().ConfigureAwait(false);
        await _cache.InvalidateAsync(entity.Id!).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public Task<bool> RemoveAsync(string identifier) => TryRemoveAsync(identifier);

    private static PolarTenantInfo ToTenantInfo(PolarTenantInfoEntity entity) => new()
    {
        Id = entity.Id ?? string.Empty,
        Identifier = entity.Identifier,
        Name = entity.Name,
        PolarAccessToken = entity.PolarAccessToken,
        Server = entity.Server,
        Status = entity.LifecycleStatus,
        SiteManagerEmail = entity.SiteManagerEmail,
        SiteManagerEmailVerified = entity.SiteManagerEmailVerified,
        SiteManagerPhone = entity.SiteManagerPhone,
    };

    private static PolarTenantInfoEntity ToEntity(PolarTenantInfo info) => new()
    {
        // Inherited from PolarTenantBase (required init):
        Id = info.Id ?? string.Empty,
        Name = info.Name ?? info.Identifier ?? "",
        Slug = info.Identifier ?? info.Id ?? "",
        CreatedAt = DateTimeOffset.UtcNow,
        // Entity-specific:
        Identifier = info.Identifier ?? string.Empty,
        PolarAccessToken = info.PolarAccessToken,
        Server = info.Server,
        LifecycleStatus = info.Status,
        SiteManagerEmail = info.SiteManagerEmail,
        SiteManagerEmailVerified = info.SiteManagerEmailVerified,
        SiteManagerPhone = info.SiteManagerPhone,
    };

}
