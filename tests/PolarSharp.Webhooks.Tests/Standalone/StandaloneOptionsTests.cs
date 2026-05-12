using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Extensions;
using Microsoft.Extensions.Logging;

namespace PolarSharp.Webhooks.Tests.Standalone;

/// <summary>
/// Verifies that <see cref="PolarWebhookOptions"/> binds correctly from
/// <c>appsettings.json</c>-style <see cref="IConfiguration"/> entries when using
/// <see cref="WebhookBuilderExtensions.AddPolarWebhooks"/> in standalone mode.
/// </summary>
public sealed class StandaloneOptionsTests
{
    // ── Default values ─────────────────────────────────────────────────────────

    [Fact]
    public void PolarWebhookOptions_DefaultPath_IsHooksPolar()
    {
        var opts = new PolarWebhookOptions();
        Assert.Equal("/hooks/polar", opts.Path);
    }

    [Fact]
    public void PolarWebhookOptions_DefaultToleranceSeconds_Is300()
    {
        var opts = new PolarWebhookOptions();
        Assert.Equal(300, opts.ToleranceSeconds);
    }

    [Fact]
    public void PolarWebhookOptions_DefaultMaxPayloadBytes_Is1MB()
    {
        var opts = new PolarWebhookOptions();
        Assert.Equal(1_048_576, opts.MaxPayloadBytes);
    }

    [Fact]
    public void PolarWebhookOptions_DefaultRequireHttps_IsTrue()
    {
        var opts = new PolarWebhookOptions();
        Assert.True(opts.RequireHttps);
    }

    [Fact]
    public void PolarWebhookOptions_DefaultFailOnMissingHandlers_IsFalse()
    {
        var opts = new PolarWebhookOptions();
        Assert.False(opts.FailOnMissingHandlers);
    }

    // ── Configuration binding from IConfiguration ──────────────────────────────

    [Fact]
    public void AddPolarWebhooks_BindsSecret_FromConfiguration()
    {
        using var sp = BuildFromConfig(new Dictionary<string, string?>
        {
            ["PolarSharp:Webhooks:Secret"] = "whsec_dGVzdEtleQ=="
        });
        var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
        Assert.Equal("whsec_dGVzdEtleQ==", opts.Secret);
    }

    [Fact]
    public void AddPolarWebhooks_BindsPath_FromConfiguration()
    {
        using var sp = BuildFromConfig(new Dictionary<string, string?>
        {
            ["PolarSharp:Webhooks:Secret"] = "dGVzdA==",
            ["PolarSharp:Webhooks:Path"]   = "/hooks/custom"
        });
        var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
        Assert.Equal("/hooks/custom", opts.Path);
    }

    [Fact]
    public void AddPolarWebhooks_BindsToleranceSeconds_FromConfiguration()
    {
        using var sp = BuildFromConfig(new Dictionary<string, string?>
        {
            ["PolarSharp:Webhooks:Secret"]           = "dGVzdA==",
            ["PolarSharp:Webhooks:ToleranceSeconds"] = "120"
        });
        var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
        Assert.Equal(120, opts.ToleranceSeconds);
    }

    [Fact]
    public void AddPolarWebhooks_BindsRequireHttps_FromConfiguration()
    {
        using var sp = BuildFromConfig(new Dictionary<string, string?>
        {
            ["PolarSharp:Webhooks:Secret"]       = "dGVzdA==",
            ["PolarSharp:Webhooks:RequireHttps"] = "false"
        });
        var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
        Assert.False(opts.RequireHttps);
    }

    [Fact]
    public void AddPolarWebhooks_BindsMaxPayloadBytes_FromConfiguration()
    {
        using var sp = BuildFromConfig(new Dictionary<string, string?>
        {
            ["PolarSharp:Webhooks:Secret"]         = "dGVzdA==",
            ["PolarSharp:Webhooks:MaxPayloadBytes"] = "524288"
        });
        var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
        Assert.Equal(524_288, opts.MaxPayloadBytes);
    }

    [Fact]
    public void AddPolarWebhooks_BindsFailOnMissingHandlers_FromConfiguration()
    {
        using var sp = BuildFromConfig(new Dictionary<string, string?>
        {
            ["PolarSharp:Webhooks:Secret"]                = "dGVzdA==",
            ["PolarSharp:Webhooks:FailOnMissingHandlers"] = "true"
        });
        var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
        Assert.True(opts.FailOnMissingHandlers);
    }

    [Fact]
    public void AddPolarWebhooks_BindsEnableRateLimiting_FromConfiguration()
    {
        using var sp = BuildFromConfig(new Dictionary<string, string?>
        {
            ["PolarSharp:Webhooks:Secret"]              = "dGVzdA==",
            ["PolarSharp:Webhooks:EnableRateLimiting"]  = "false"
        });
        var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
        Assert.False(opts.EnableRateLimiting);
    }

    // ── Multiple secrets (rotation) ────────────────────────────────────────────

    [Fact]
    public void AddPolarWebhooks_BindsMultipleSecrets_FromConfiguration()
    {
        using var sp = BuildFromConfig(new Dictionary<string, string?>
        {
            ["PolarSharp:Webhooks:Secrets:0"] = "whsec_bmV3U2VjcmV0",
            ["PolarSharp:Webhooks:Secrets:1"] = "whsec_b2xkU2VjcmV0"
        });
        var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
        Assert.Equal(2, opts.Secrets.Count);
        Assert.Contains("whsec_bmV3U2VjcmV0", opts.Secrets);
        Assert.Contains("whsec_b2xkU2VjcmV0", opts.Secrets);
    }

    [Fact]
    public void PolarWebhookOptions_GetSecrets_CombinesSingleSecretAndList()
    {
        var opts = new PolarWebhookOptions
        {
            Secret  = "whsec_c2luZ2xl",
            Secrets = ["whsec_bGlzdE9uZQ==", "whsec_bGlzdFR3bw=="]
        };
        var secrets = opts.GetSecrets().ToList();
        Assert.Equal(3, secrets.Count);
    }

    [Fact]
    public void PolarWebhookOptions_GetSecrets_IgnoresNullOrWhitespaceEntries()
    {
        var opts = new PolarWebhookOptions
        {
            Secret  = "",           // empty — should be ignored
            Secrets = ["  ", "whsec_dmFsaWQ="]  // whitespace + valid
        };
        var secrets = opts.GetSecrets().ToList();
        Assert.Single(secrets);
    }

    // ── In-code configure override ─────────────────────────────────────────────

    [Fact]
    public void AddPolarWebhooks_InCodeConfigure_OverridesSecret()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddPolarWebhooks(opts =>
        {
            opts.Secret       = "whsec_Y29kZU92ZXJyaWRl";
            opts.RequireHttps = false;
        });

        using var sp  = services.BuildServiceProvider();
        var opts      = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
        Assert.Equal("whsec_Y29kZU92ZXJyaWRl", opts.Secret);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static ServiceProvider BuildFromConfig(Dictionary<string, string?> configValues)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        // AddPolarWebhooks already calls BindConfiguration("PolarSharp:Webhooks") internally;
        // do not call it again here or options will bind twice and produce duplicate entries.
        services.AddPolarWebhooks();

        return services.BuildServiceProvider();
    }
}
