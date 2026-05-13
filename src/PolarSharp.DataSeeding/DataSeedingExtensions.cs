using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.DataSeeding.Generators;
using PolarSharp.DataSeeding.Sync;

namespace PolarSharp.DataSeeding;

/// <summary>DI registration for <c>PolarSharp.DataSeeding</c>.</summary>
public static class DataSeedingExtensions
{
    /// <summary>
    /// Registers the seeder, generators, sync service, and the no-op default
    /// <see cref="ISeedSink"/>. Hosts override the sink registration to wire persistence to
    /// their catalog DbContext.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Developer note (AOT publish):</strong> the bundled <c>PolarTestApp</c> does
    /// NOT transitively reference <c>PolarSharp.DataSeeding</c>, so <c>dotnet publish
    /// -p:PublishAot=true</c> against the test app stays clean. The <c>Bogus</c> faker
    /// library uses some reflection internally — hosts who publish AOT with
    /// <c>PolarSharp.DataSeeding</c> installed may see reflection / trim warnings. Two
    /// supported mitigations: (1) suppress the warnings only in the host's csproj via
    /// <c>&lt;TrimmerRootAssembly Include="Bogus" /&gt;</c>, or (2) gate the
    /// <c>AddPolarDataSeeding(...)</c> registration behind <c>#if DEBUG</c> so the package
    /// compiles out of the Production build entirely. <c>PolarSharp.DataSeeding</c> is a
    /// dev-time package — designed for sandbox / QA / demo environments, not production
    /// hot paths — so either approach is acceptable.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPolarDataSeeding(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<PolarDataSeedingOptions>()
            .Bind(configuration.GetSection(PolarDataSeedingOptions.SectionName))
            .ValidateOnStart();

        // Generators (stateless, singletons).
        services.TryAddSingleton<FakeProductGenerator>();
        services.TryAddSingleton<FakeCategoryGenerator>();
        services.TryAddSingleton<FakeDepartmentGenerator>();
        services.TryAddSingleton<FakeLicenseKeysBenefitGenerator>();
        services.TryAddSingleton<FakeDiscountGenerator>();
        services.TryAddSingleton<FakeCheckoutLinkGenerator>();

        // Default sink — host overrides this when wiring the catalog DbContext.
        services.TryAddSingleton<ISeedSink, CountingNoOpSeedSink>();

        // Orchestrator.
        services.TryAddScoped<IPolarDataSeeder, PolarDataSeeder>();

        // Toggle-event plumbing.
        services.TryAddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PolarDataSeedingOptions>>().Value;
            return Channel.CreateBounded<FakeDataToggleChanged>(new BoundedChannelOptions(opts.ToggleChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
        });
        services.TryAddSingleton<IFakeDataToggleNotifier, ChannelFakeDataToggleNotifier>();
        services.AddHostedService<FakeDataSyncService>();

        return services;
    }
}
