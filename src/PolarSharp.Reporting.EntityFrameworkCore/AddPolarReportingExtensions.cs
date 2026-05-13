using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.EntityFrameworkCore;

/// <summary>
/// Top-level v1.3.G orchestrator that composes every EF-backed
/// <c>PolarSharp.Reporting</c> service registration into a single one-line call.
/// </summary>
/// <remarks>
/// Hosts must still register the reporting DbContext separately via the provider package's
/// <c>UseSqliteReporting</c> / <c>UseSqlServerReporting</c> / <c>UsePostgreSqlReporting</c>
/// extension before or after this call.
/// </remarks>
public static class AddPolarReportingExtensions
{
    /// <summary>
    /// Registers the full <c>PolarSharp.Reporting</c> service surface: the
    /// <see cref="IPolarReportingClient"/> backed by EF over the local snapshot tables, plus
    /// the snapshot ingestion service. Binds <c>PolarReportingOptions</c> from the
    /// supplied configuration.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">Application configuration containing the
    /// <c>PolarSharp:Reporting</c> options section.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPolarReporting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddPolarReportingSnapshot(configuration);
        services.AddScoped<IPolarReportingClient, EfPolarReportingClient>();
        services.AddScoped<IAdvancedReportingClient, EfAdvancedReportingClient>();
        return services;
    }
}
