using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement;
using PolarSharp.EcommerceStoreManagement.Translation;
using PolarSharp.EcommerceStoreManagement.Translation.Anthropic;
using PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI;
using PolarSharp.EcommerceStoreManagement.Translation.Gemini;
using PolarSharp.EcommerceStoreManagement.Translation.Grok;
using PolarSharp.EcommerceStoreManagement.Translation.OpenAI;

namespace PolarSharp.EcommerceStoreManagement.Translation.Tests;

/// <summary>
/// Verifies each provider's <c>UseXxxTranslator()</c> extension registers an
/// <see cref="IPolarCatalogTranslatorFactory"/> with the right <see cref="TranslationProvider"/>
/// discriminator AND that the factory's <see cref="IPolarCatalogTranslatorFactory.Create"/>
/// returns a usable translator instance.
/// </summary>
public sealed class FactoryRegistrationTests
{
    [Fact]
    public void Anthropic_registration_yields_factory_with_Anthropic_discriminator()
    {
        var factory = ResolveFactory(s => s.UseAnthropicTranslator());
        Assert.Equal(TranslationProvider.Anthropic, factory.Provider);
        Assert.IsType<AnthropicCatalogTranslator>(factory.Create("k", null, null));
    }

    [Fact]
    public void OpenAI_registration_yields_factory_with_OpenAI_discriminator()
    {
        var factory = ResolveFactory(s => s.UseOpenAiTranslator());
        Assert.Equal(TranslationProvider.OpenAI, factory.Provider);
        Assert.IsType<OpenAiCatalogTranslator>(factory.Create("k", null, null));
    }

    [Fact]
    public void AzureOpenAI_registration_yields_factory_with_AzureOpenAI_discriminator()
    {
        var factory = ResolveFactory(s => s.UseAzureOpenAiTranslator());
        Assert.Equal(TranslationProvider.AzureOpenAI, factory.Provider);
        // Azure requires both model (deployment) AND endpoint — verifying the factory throws cleanly when missing.
        Assert.Throws<ArgumentException>(() => factory.Create("k", null, null));
        Assert.IsType<AzureOpenAiCatalogTranslator>(factory.Create("k", "deployment", "https://x.openai.azure.com"));
    }

    [Fact]
    public void Gemini_registration_yields_factory_with_Gemini_discriminator()
    {
        var factory = ResolveFactory(s => s.UseGeminiTranslator());
        Assert.Equal(TranslationProvider.Gemini, factory.Provider);
        Assert.IsType<GeminiCatalogTranslator>(factory.Create("k", null, null));
    }

    [Fact]
    public void Grok_registration_yields_factory_with_Grok_discriminator()
    {
        var factory = ResolveFactory(s => s.UseGrokTranslator());
        Assert.Equal(TranslationProvider.Grok, factory.Provider);
        Assert.IsType<GrokCatalogTranslator>(factory.Create("k", null, null));
    }

    [Fact]
    public void Multiple_providers_can_register_simultaneously_for_per_tenant_resolution()
    {
        // The 3-tier resolver picks whichever provider matches the tenant's chosen TranslationProvider
        // — so a host may want to register several factories at once. Each must be discoverable.
        var services = new ServiceCollection();
        services.UseAnthropicTranslator();
        services.UseOpenAiTranslator();
        services.UseGeminiTranslator();
        services.UseGrokTranslator();
        var sp = services.BuildServiceProvider();

        var factories = sp.GetServices<IPolarCatalogTranslatorFactory>().ToList();
        Assert.Equal(4, factories.Count);
        Assert.Contains(factories, f => f.Provider == TranslationProvider.Anthropic);
        Assert.Contains(factories, f => f.Provider == TranslationProvider.OpenAI);
        Assert.Contains(factories, f => f.Provider == TranslationProvider.Gemini);
        Assert.Contains(factories, f => f.Provider == TranslationProvider.Grok);
    }

    [Fact]
    public void Configure_callback_sets_master_options_for_Tier_2_resolution()
    {
        var services = new ServiceCollection();
        services.UseAnthropicTranslator(opts =>
        {
            opts.ApiKey = "master-anthropic-key";
            opts.Model = "claude-sonnet-4-6";
        });
        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<EcommerceTranslationMasterOptions>>().Value;
        Assert.Equal(TranslationProvider.Anthropic, options.Provider);
        Assert.Equal("master-anthropic-key", options.ApiKey);
        Assert.Equal("claude-sonnet-4-6", options.Model);
    }

    private static IPolarCatalogTranslatorFactory ResolveFactory(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        return services.BuildServiceProvider().GetRequiredService<IPolarCatalogTranslatorFactory>();
    }
}
