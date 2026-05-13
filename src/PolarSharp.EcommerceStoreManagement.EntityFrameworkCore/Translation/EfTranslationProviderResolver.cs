using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Translation;

/// <summary>
/// Default <see cref="ITranslationProviderResolver"/> implementation. Performs the
/// documented 3-tier resolution against an <see cref="ITenantTranslationConfigLookup"/>
/// (typically EF-backed) for the per-tenant tier and
/// <see cref="EcommerceTranslationMasterOptions"/> for the master tier.
/// </summary>
/// <remarks>
/// Resolution order, first match wins:
/// <list type="number">
///   <item><description><b>Tier 1 — per-tenant:</b> the current tenant's stored config has a
///     non-<see cref="TranslationProvider.None"/> provider, a present encrypted API key,
///     a registered factory for the provider, and the key decrypts successfully.</description></item>
///   <item><description><b>Tier 2 — master / SaaS-site:</b>
///     <see cref="EcommerceTranslationMasterOptions"/> has a non-None provider, a non-empty
///     API key, and a registered factory.</description></item>
///   <item><description><b>Tier 3 — disabled:</b> nothing matches; the resolver returns
///     <see langword="null"/> and translation features gracefully no-op.</description></item>
/// </list>
/// Every fall-through reason emits a Debug or Warning log; plaintext API keys never appear
/// in any log entry. Decryption failures fall through to the master tier quietly.
/// </remarks>
internal sealed class EfTranslationProviderResolver(
    ITenantTranslationConfigLookup tenantLookup,
    IEnumerable<IPolarCatalogTranslatorFactory> factories,
    IDataProtectionProvider dataProtection,
    IOptionsMonitor<EcommerceTranslationMasterOptions> masterOptions,
    ILogger<EfTranslationProviderResolver> logger) : ITranslationProviderResolver
{
    private readonly ITenantTranslationConfigLookup _tenantLookup =
        tenantLookup ?? throw new ArgumentNullException(nameof(tenantLookup));
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
        var config = await _tenantLookup.GetAsync(ct).ConfigureAwait(false);
        if (config is null || config.Provider == TranslationProvider.None)
            return null;

        if (string.IsNullOrEmpty(config.EncryptedApiKey))
        {
            _logger.LogDebug(
                "Translation resolver: tenant {TenantId} has provider {Provider} but no encrypted API key. Falling through to master.",
                config.TenantId, config.Provider);
            return null;
        }

        var factory = FindFactory(config.Provider);
        if (factory is null)
        {
            _logger.LogWarning(
                "Translation resolver: tenant {TenantId} configured provider {Provider} but no IPolarCatalogTranslatorFactory is registered. Falling through to master.",
                config.TenantId, config.Provider);
            return null;
        }

        string apiKey;
        try
        {
            var protector = _dataProtection.ForTranslationApiKey();
            apiKey = protector.Unprotect(config.EncryptedApiKey);
        }
        catch (Exception ex)
        {
            // Decryption failures (key ring rotated without re-encrypting, tampered ciphertext, etc.)
            // never surface plaintext. Fall through to master config quietly — the operator will
            // see the warning log.
            _logger.LogWarning(ex,
                "Translation resolver: tenant {TenantId} translation API key decryption failed. Falling through to master.",
                config.TenantId);
            return null;
        }

        return factory.Create(apiKey, config.Model, config.Endpoint);
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
