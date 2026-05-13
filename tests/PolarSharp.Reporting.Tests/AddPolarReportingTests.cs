using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;
using PolarSharp.Reporting.Snapshot;
using PolarSharp.Reporting.Tests.Infrastructure;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Verifies the v1.3.G <c>AddPolarReporting</c> orchestrator registers the snapshot
/// service and the EF-backed reporting client in a single one-line call.
/// </summary>
public sealed class AddPolarReportingTests
{
    /// <summary>Pre-registers a stub Polar HTTP API so the orchestrator's PolarClient-backed default doesn't try to instantiate PolarClient.</summary>
    private static void ConfigureWithStubApi(IServiceCollection s)
    {
        s.AddScoped<IPolarReportingApi, StubReportingApi>();
        s.AddPolarReporting(EmptyConfiguration());
    }

    [Fact]
    public async Task AddPolarReporting_registers_snapshot_service_and_reporting_client()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: ConfigureWithStubApi);

        using var scope = ctx.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IReportSnapshotService>());
        Assert.NotNull(scope.ServiceProvider.GetService<IPolarReportingApi>());
        Assert.NotNull(scope.ServiceProvider.GetService<IPolarReportingClient>());
    }

    [Fact]
    public async Task AddPolarReporting_is_idempotent_when_called_twice()
    {
        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: s =>
        {
            ConfigureWithStubApi(s);
            s.AddPolarReporting(EmptyConfiguration());
        });

        using var scope = ctx.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IReportSnapshotService>());
    }

    private static IConfiguration EmptyConfiguration() =>
        new ConfigurationBuilder().Build();

    private sealed class StubReportingApi : IPolarReportingApi
    {
        public Task<Result<IReadOnlyList<EventPayload>, PolarReportingApiError>> FetchEventsSinceAsync(string? s, int n, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<OrderPayload>, PolarReportingApiError>> FetchOrdersSinceAsync(string? s, int n, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<SubscriptionPayload>, PolarReportingApiError>> FetchSubscriptionsSinceAsync(string? s, int n, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<CustomerPayload>, PolarReportingApiError>> FetchCustomersSinceAsync(string? s, int n, CancellationToken c) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<BenefitGrantPayload>, PolarReportingApiError>> FetchBenefitGrantsSinceAsync(string? s, int n, CancellationToken c) => throw new NotImplementedException();
    }
}
