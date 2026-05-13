using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.Reporting.Snapshot;

namespace PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

/// <summary>
/// DI registration for the v1.3.F reporting snapshot service plus its default Polar HTTP
/// wrapper.
/// </summary>
/// <remarks>
/// Hosts wanting different Polar HTTP behaviour (sandbox testing, custom retry policies,
/// or a short-circuit of the deferred TASK-V20-005 implementation) can register their own
/// <see cref="IPolarReportingApi"/> before calling this method — the <c>TryAdd</c>
/// registration here leaves it in place.
/// </remarks>
public static class SnapshotServicesExtensions
{
    /// <summary>
    /// Registers <see cref="IReportSnapshotService"/> + the Polar HTTP wrapper + binds
    /// <see cref="PolarReportingOptions"/> from <c>PolarSharp:Reporting</c>.
    /// </summary>
    public static IServiceCollection AddPolarReportingSnapshot(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PolarReportingOptions>(
            configuration.GetSection(PolarReportingOptions.SectionName));

        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);
        services.TryAddScoped<IPolarReportingApi, PolarClientReportingApi>();
        services.AddScoped<IReportSnapshotService, ReportSnapshotService>();

        return services;
    }
}
