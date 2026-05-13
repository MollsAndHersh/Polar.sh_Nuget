using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Translation;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Locks the documented 3-tier translation provider resolution contract:
/// per-tenant → master / SaaS-site → disabled. The resolver depends on an
/// <see cref="ITenantTranslationConfigLookup"/> abstraction; these tests inject a stub
/// so no EF Core DbContext or Finbuckle multi-tenant scope is required.
/// </summary>
public sealed class EfTranslationProviderResolverTests
{
    private const string TenantId = "tenant-acme";

    [Fact]
    public async Task Per_tenant_config_with_valid_factory_and_key_returns_per_tenant_translator()
    {
        var dp = new EphemeralDataProtectionProvider();
        var encrypted = dp.ForTranslationApiKey().Protect("sk-tenant-real");

        var perTenantFactory = new SpyTranslatorFactory(TranslationProvider.Anthropic);
        var masterFactory = new SpyTranslatorFactory(TranslationProvider.OpenAI);

        var resolver = NewResolver(
            tenantConfig: new TenantTranslationConfig(TenantId, TranslationProvider.Anthropic, encrypted, "claude-sonnet-4-6", null),
            masterOptions: MasterWith(TranslationProvider.OpenAI, "sk-master"),
            factories: [perTenantFactory, masterFactory],
            dataProtection: dp);

        var result = await resolver.ResolveAsync();

        Assert.NotNull(result);
        Assert.Single(perTenantFactory.Calls);
        Assert.Empty(masterFactory.Calls);
        Assert.Equal("sk-tenant-real", perTenantFactory.Calls[0].ApiKey);
        Assert.Equal("claude-sonnet-4-6", perTenantFactory.Calls[0].Model);
    }

    [Fact]
    public async Task Per_tenant_provider_set_but_no_encrypted_key_falls_through_to_master()
    {
        var masterFactory = new SpyTranslatorFactory(TranslationProvider.OpenAI);

        var resolver = NewResolver(
            tenantConfig: new TenantTranslationConfig(TenantId, TranslationProvider.Anthropic, EncryptedApiKey: null, Model: null, Endpoint: null),
            masterOptions: MasterWith(TranslationProvider.OpenAI, "sk-master"),
            factories: [masterFactory]);

        var result = await resolver.ResolveAsync();

        Assert.NotNull(result);
        Assert.Single(masterFactory.Calls);
        Assert.Equal("sk-master", masterFactory.Calls[0].ApiKey);
    }

    [Fact]
    public async Task Per_tenant_provider_None_falls_through_to_master()
    {
        var dp = new EphemeralDataProtectionProvider();
        var encrypted = dp.ForTranslationApiKey().Protect("sk-leftover");
        var masterFactory = new SpyTranslatorFactory(TranslationProvider.OpenAI);

        var resolver = NewResolver(
            // Provider=None means "use master", even if an old encrypted key is lying around in the row.
            tenantConfig: new TenantTranslationConfig(TenantId, TranslationProvider.None, encrypted, null, null),
            masterOptions: MasterWith(TranslationProvider.OpenAI, "sk-master"),
            factories: [masterFactory],
            dataProtection: dp);

        var result = await resolver.ResolveAsync();

        Assert.NotNull(result);
        Assert.Single(masterFactory.Calls);
    }

    [Fact]
    public async Task Per_tenant_factory_not_registered_falls_through_to_master()
    {
        var dp = new EphemeralDataProtectionProvider();
        var encrypted = dp.ForTranslationApiKey().Protect("sk-tenant-real");
        var masterFactory = new SpyTranslatorFactory(TranslationProvider.OpenAI);

        var resolver = NewResolver(
            // Tenant wants Anthropic, but host only registered the OpenAI factory.
            tenantConfig: new TenantTranslationConfig(TenantId, TranslationProvider.Anthropic, encrypted, "claude-sonnet-4-6", null),
            masterOptions: MasterWith(TranslationProvider.OpenAI, "sk-master"),
            factories: [masterFactory],
            dataProtection: dp);

        var result = await resolver.ResolveAsync();

        Assert.NotNull(result);
        Assert.Single(masterFactory.Calls);
        Assert.Equal("sk-master", masterFactory.Calls[0].ApiKey);
    }

    [Fact]
    public async Task Per_tenant_key_decryption_failure_falls_through_to_master_without_leaking_plaintext()
    {
        var perTenantFactory = new SpyTranslatorFactory(TranslationProvider.Anthropic);
        var masterFactory = new SpyTranslatorFactory(TranslationProvider.OpenAI);

        var resolver = NewResolver(
            // Ciphertext that cannot be decrypted with an ephemeral key ring.
            tenantConfig: new TenantTranslationConfig(TenantId, TranslationProvider.Anthropic, "not-real-ciphertext", null, null),
            masterOptions: MasterWith(TranslationProvider.OpenAI, "sk-master"),
            factories: [perTenantFactory, masterFactory]);

        var result = await resolver.ResolveAsync();

        Assert.NotNull(result);
        Assert.Empty(perTenantFactory.Calls);                   // Per-tenant factory never invoked
        Assert.Single(masterFactory.Calls);                     // Fell through to master
        Assert.Equal("sk-master", masterFactory.Calls[0].ApiKey);
    }

    [Fact]
    public async Task Master_only_configured_returns_master_translator()
    {
        var masterFactory = new SpyTranslatorFactory(TranslationProvider.OpenAI);

        var resolver = NewResolver(
            tenantConfig: null,                                  // No per-tenant profile row
            masterOptions: MasterWith(TranslationProvider.OpenAI, "sk-master", model: "gpt-4o"),
            factories: [masterFactory]);

        var result = await resolver.ResolveAsync();

        Assert.NotNull(result);
        Assert.Single(masterFactory.Calls);
        Assert.Equal("sk-master", masterFactory.Calls[0].ApiKey);
        Assert.Equal("gpt-4o", masterFactory.Calls[0].Model);
    }

    [Fact]
    public async Task Master_factory_not_registered_returns_null()
    {
        // Master selects OpenAI but no OpenAI factory is registered.
        var resolver = NewResolver(
            tenantConfig: null,
            masterOptions: MasterWith(TranslationProvider.OpenAI, "sk-master"),
            factories: []);

        var result = await resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task Master_provider_None_returns_null()
    {
        var resolver = NewResolver(
            tenantConfig: null,
            masterOptions: new EcommerceTranslationMasterOptions { Provider = TranslationProvider.None, ApiKey = "sk-ignored" },
            factories: [new SpyTranslatorFactory(TranslationProvider.Anthropic)]);

        var result = await resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task Master_provider_set_but_ApiKey_empty_returns_null()
    {
        var resolver = NewResolver(
            tenantConfig: null,
            masterOptions: new EcommerceTranslationMasterOptions { Provider = TranslationProvider.OpenAI, ApiKey = "  " },
            factories: [new SpyTranslatorFactory(TranslationProvider.OpenAI)]);

        var result = await resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task No_tenant_config_and_no_master_returns_null()
    {
        var resolver = NewResolver(
            tenantConfig: null,
            masterOptions: new EcommerceTranslationMasterOptions(),    // defaults: Provider=None, ApiKey=null
            factories: []);

        var result = await resolver.ResolveAsync();

        Assert.Null(result);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────

    private static EfTranslationProviderResolver NewResolver(
        TenantTranslationConfig? tenantConfig,
        EcommerceTranslationMasterOptions masterOptions,
        IReadOnlyList<IPolarCatalogTranslatorFactory> factories,
        IDataProtectionProvider? dataProtection = null)
    {
        var lookup = new StubLookup(tenantConfig);
        var monitor = new StubOptionsMonitor<EcommerceTranslationMasterOptions>(masterOptions);
        return new EfTranslationProviderResolver(
            lookup,
            factories,
            dataProtection ?? new EphemeralDataProtectionProvider(),
            monitor,
            NullLogger<EfTranslationProviderResolver>.Instance);
    }

    private static EcommerceTranslationMasterOptions MasterWith(TranslationProvider provider, string apiKey, string? model = null) =>
        new() { Provider = provider, ApiKey = apiKey, Model = model };

    private sealed class StubLookup(TenantTranslationConfig? config) : ITenantTranslationConfigLookup
    {
        public Task<TenantTranslationConfig?> GetAsync(CancellationToken ct = default) => Task.FromResult(config);
    }

    private sealed class StubOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue { get; } = currentValue;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class SpyTranslatorFactory(TranslationProvider provider) : IPolarCatalogTranslatorFactory
    {
        public TranslationProvider Provider { get; } = provider;
        public List<(string ApiKey, string? Model, string? Endpoint)> Calls { get; } = [];

        public IPolarCatalogTranslator Create(string apiKey, string? model, string? endpoint)
        {
            Calls.Add((apiKey, model, endpoint));
            return new NoOpCatalogTranslator();
        }
    }
}
