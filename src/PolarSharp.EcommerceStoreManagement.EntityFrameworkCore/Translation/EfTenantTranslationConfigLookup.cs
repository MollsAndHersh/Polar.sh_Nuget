using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Translation;

/// <summary>
/// EF Core-backed <see cref="ITenantTranslationConfigLookup"/>. Reads
/// <see cref="TenantBusinessProfileEntity"/> from <see cref="PolarCatalogDbContext"/>
/// projecting only the translation-relevant columns.
/// </summary>
/// <remarks>
/// The DbContext's global tenant query filter scopes the read to the current tenant
/// automatically. When no current-tenant context is in scope (background work without
/// scope hydration), the DbContext throws <see cref="InvalidOperationException"/> from
/// the global query filter; this lookup swallows that and returns <see langword="null"/>
/// so the resolver falls through to the master / disabled tiers gracefully.
/// </remarks>
internal sealed class EfTenantTranslationConfigLookup(
    PolarCatalogDbContext db,
    ILogger<EfTenantTranslationConfigLookup> logger) : ITenantTranslationConfigLookup
{
    private readonly PolarCatalogDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly ILogger<EfTenantTranslationConfigLookup> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<TenantTranslationConfig?> GetAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Set<TenantBusinessProfileEntity>()
                .AsNoTracking()
                .Select(p => new TenantTranslationConfig(
                    p.TenantId,
                    p.TranslationProvider,
                    p.TranslationApiKeyEncrypted,
                    p.TranslationModel,
                    p.TranslationEndpoint))
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            // Most commonly: no current tenant in scope, so the global query filter cannot
            // evaluate. Quiet fallthrough — the resolver handles null by trying master config.
            _logger.LogDebug(ex, "Tenant translation config lookup skipped (no current tenant context).");
            return null;
        }
    }
}
