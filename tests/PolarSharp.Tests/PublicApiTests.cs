using PublicApiGenerator;
using VerifyXunit;

namespace PolarSharp.Tests;

/// <summary>
/// Locks down the public API surface of each library assembly. A test failure means the
/// public API changed — review the diff and either accept it (copy .received.txt →
/// .verified.txt and commit) or revert the unintentional change.
/// </summary>
public class PublicApiTests : VerifyBase
{
    public PublicApiTests() : base() { }

    private static readonly ApiGeneratorOptions Options = new()
    {
        IncludeAssemblyAttributes = false,
    };

    [Fact]
    public Task PolarSharp_PublicApi_HasNotChanged()
    {
        var api = typeof(PolarClient).Assembly.GeneratePublicApi(Options);
        return Verify(api)
            .UseDirectory("ApiSnapshots")
            .UseFileName("PolarSharp");
    }

    [Fact]
    public Task PolarSharpWebhooks_PublicApi_HasNotChanged()
    {
        var api = typeof(PolarSharp.Webhooks.WebhookValidator).Assembly.GeneratePublicApi(Options);
        return Verify(api)
            .UseDirectory("ApiSnapshots")
            .UseFileName("PolarSharp.Webhooks");
    }

    [Fact]
    public Task PolarSharpMultiTenant_PublicApi_HasNotChanged()
    {
        var api = typeof(PolarSharp.MultiTenant.PolarTenantInfo).Assembly.GeneratePublicApi(Options);
        return Verify(api)
            .UseDirectory("ApiSnapshots")
            .UseFileName("PolarSharp.MultiTenant");
    }
}
