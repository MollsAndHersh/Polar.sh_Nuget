using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Translation;

/// <summary>
/// Default <see cref="ITranslationProviderResolver"/> implementation that performs the
/// documented 3-tier resolution against the catalog DbContext.
/// </summary>
/// <remarks>
/// Resolution order, first match wins:
/// <list type="number">
///   <item><description><b>Tier 1 — per-tenant:</b> if the current tenant's
///     <see cref="TenantBusinessProfileEntity.TranslationProvider"/> is not
///     <see cref="TranslationProvider.None"/> and an encrypted API key is present, decrypt
///     it via Data Protection and resolve a matching <see cref="IPolarCatalogTranslatorFactory"/>.</description></item>
///   <item><description><b>Tier 2 — master / SaaS-site:</b> fall back to
///     <see cref="EcommerceTranslationMasterOptions"/> when the master has a configured
///     provider + API key and a matching factory is registered.</description></item>
///   <item><description><b>Tier 3 — disabled:</b> return <see langword="null"/>.
///     Translation features gracefully no-op.</description></item>
/// </list>
/// The DbContext's global query filter scopes the
/// <see cref="TenantBusinessProfileEntity"/> read to the current tenant automatically — no
/// explicit tenant id parameter is needed.
/// </remarks>
internal sealed class EfTranslationProviderResolver(
    PolarCatalogDbContext db,
    IEnumerable<IPolarCatalogTranslatorFactory> factories,
    IDataProtectionProvider dataProtection,
    IOptionsMonitor<EcommerceTranslationMasterOptions> masterOptions,
    ILogger<EfTranslationProviderResolver> logger) : ITranslationProviderResolver
{
    private readonly PolarCatalogDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly IReadOnlyList<IPolarCatalogTranslatorFactory> _factories =
        (factories ?? throw new ArgumentNullException(nameof(factories))).ToList();
    private readonly IDataProtectionProvider _dataProtection =
        dataProtection ?? throw new ArgumentNullException(nameof(dataProtection));
    private readonly IOptionsMonitor<EcommerceTranslationMasterOptions> _masterOptions =
        masterOptions ?? throw new ArgumentNullException(nameof(masterOptions));
    private readonly ILogger<EfTranslationProviderResolver> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<IPolarCatalogTranslator?> ResolveAsync(CancellationToken ct = default)
    {
        if (await TryResolvePerTenantAsync(ct).ConfigureAwait(false) is { } perTenant)
            return perTenant;

        if (TryResolveMaster() is { } master)
            return master;

        return null;
    }

    private async Task<IPolarCatalogTranslator?> TryResolvePerTenantAsync(CancellationToken ct)
    {
        TenantBusinessProfileEntity? profile;
        try
        {
            profile = await _db.Set<TenantBusinessProfileEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            // Most commonly: no current tenant in scope, so the global query filter cannot evaluate.
            _logger.LogDebug(ex, "Translation resolver: per-tenant lookup skipped (no current tenant context).");
            return null;
        }

        if (profile is null || profile.TranslationProvider == TranslationProvider.None)
            return null;

        if (string.IsNullOrEmpty(profile.TranslationApiKeyEncrypted))
        {
            _logger.LogDebug(
                "Translation resolver: tenant {TenantId} has provider {Provider} but no encrypted API key. Falling through to master.",
                profile.TenantId, profile.TranslationProvider);
            return null;
        }

        var factory = FindFactory(profile.TranslationProvider);
        if (factory is null)
        {
            _logger.LogWarning(
                "Translation resolver: tenant {TenantId} configured provider {Provider} but no IPolarCatalogTranslatorFactory is registered. Falling through to master.",
                profile.TenantId, profile.TranslationProvider);
            return null;
        }

        string apiKey;
        try
        {
            var protector = _dataProtection.ForTranslationApiKey();
            apiKey = protector.Unprotect(profile.TranslationApiKeyEncrypted);
        }
        catch (Exception ex)
        {
            // Decryption failures (key ring rotated without re-encrypting, tampered ciphertext, etc.)
            // never surface plaintext. Fall through to master config quietly — the operator will
            // see the warning log.
            _logger.LogWarning(ex,
                "Translation resolver: tenant {TenantId} translation API key decryption failed. Falling through to master.",
                profile.TenantId);
            return null;
        }

        return factory.Create(apiKey, profile.TranslationModel, profile.TranslationEndpoint);
    }

    private IPolarCatalogTranslator? TryResolveMaster()
    {
        var master = _masterOptions.CurrentValue;
        if (master.Provider == TranslationProvider.None || string.IsNullOrWhiteSpace(master.ApiKey))
            return null;

        var factory = FindFactory(master.Provider);
        if (factory is null)
        {
            _logger.LogWarning(
                "Translation resolver: master config selects {Provider} but no matching IPolarCatalogTranslatorFactory is registered. Translation disabled for this request.",
                master.Provider);
            return null;
        }

        return factory.Create(master.ApiKey, master.Model, master.Endpoint);
    }

    private IPolarCatalogTranslatorFactory? FindFactory(TranslationProvider provider)
    {
        for (var i = 0; i < _factories.Count; i++)
            if (_factories[i].Provider == provider)
                return _factories[i];
        return null;
    }
}
