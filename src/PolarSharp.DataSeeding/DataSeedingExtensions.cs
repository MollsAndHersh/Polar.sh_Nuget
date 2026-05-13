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
