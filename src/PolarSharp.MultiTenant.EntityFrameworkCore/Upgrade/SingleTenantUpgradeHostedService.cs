using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

/// <summary>
/// Hosted service that orchestrates the single-tenant -> multi-tenant data upgrade on the
/// first multi-tenant-mode startup. Resolves the default tenant per the configured
/// <see cref="DefaultTenantStrategy"/>, upserts it into the registry, then invokes the
/// per-provider <see cref="ISingleTenantUpgradeMigrator"/> to stamp existing rows.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Run-once semantics</strong> are delegated to the provider migrator: each
/// implementation persists a completion marker the orchestrator reads via
/// <see cref="ISingleTenantUpgradeMigrator.HasUpgradeCompletedAsync(CancellationToken)"/>
/// on subsequent boots.
/// </para>
/// <para>
/// <strong>Failure posture.</strong> Configuration / setup failures (no migrator registered,
/// no resolver for HostSupplied strategy, validator failures) cause the hosted service to
/// throw from <see cref="StartAsync(CancellationToken)"/>, blocking host startup. This is
/// deliberate: the host cannot safely begin serving multi-tenant traffic while data is in
/// a half-stamped state. Operators who want to defer the upgrade should set
/// <see cref="SingleTenantUpgradeOptions.EnableAutomaticUpgrade"/> to <see langword="false"/>
/// and run the upgrade out-of-band via the CLI.
/// </para>
/// </remarks>
internal sealed class SingleTenantUpgradeHostedService : IHostedService
{
    private readonly IServiceProvider _rootServices;
    private readonly IOptions<SingleTenantUpgradeOptions> _options;
    private readonly ILogger<SingleTenantUpgradeHostedService> _logger;

    /// <summary>Initializes a new <see cref="SingleTenantUpgradeHostedService"/>.</summary>
    /// <param name="rootServices">The root service provider — used to create the per-run scope.</param>
    /// <param name="options">The bound upgrade options.</param>
    /// <param name="logger">Logger.</param>
    public SingleTenantUpgradeHostedService(
        IServiceProvider rootServices,
        IOptions<SingleTenantUpgradeOptions> options,
        ILogger<SingleTenantUpgradeHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(rootServices);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _rootServices = rootServices;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;

        if (!opts.EnableAutomaticUpgrade)
        {
            _logger.LogInformation(
                "PolarSharp single-tenant -> MT upgrade: automatic upgrade disabled " +
                "(SingleTenantUpgrade:EnableAutomaticUpgrade=false). " +
                "Run `dotnet polar-mt upgrade` manually when ready.");
            return;
        }

        await using var scope = _rootServices.CreateAsyncScope();
        var migrator = scope.ServiceProvider.GetService<ISingleTenantUpgradeMigrator>()
            ?? throw new InvalidOperationException(
                "PolarSharp single-tenant -> MT upgrade: no ISingleTenantUpgradeMigrator " +
                "implementation registered. Each provider package (SqlServer / Sqlite / " +
                "PostgreSQL / MariaDb / Cosmos) registers its own; ensure the matching " +
                "provider's .Use*Upgrade() extension was called during AddPolarMultiTenant " +
                "configuration.");

        // Honour the configured ceiling — combine with the host's shutdown token so a slow
        // upgrade does not also block graceful shutdown indefinitely.
        using var ceiling = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ceiling.CancelAfter(opts.MaxRunDuration);
        var ct = ceiling.Token;

        if (await migrator.HasUpgradeCompletedAsync(ct).ConfigureAwait(false))
        {
            _logger.LogInformation(
                "PolarSharp single-tenant -> MT upgrade: completion marker present — skipping. " +
                "Provider migrator '{Migrator}' reports the upgrade is already complete.",
                migrator.GetType().FullName);
            return;
        }

        var defaultTenant = await ResolveDefaultTenantAsync(scope.ServiceProvider, opts, ct).ConfigureAwait(false);

        var registryUpgrader = scope.ServiceProvider.GetRequiredService<ITenantRegistryUpgrader>();
        var persistedTenant = await registryUpgrader.UpsertAsync(defaultTenant, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "PolarSharp single-tenant -> MT upgrade: starting. Strategy={Strategy} " +
            "DefaultTenantId={TenantId} DefaultTenantSlug={Slug} MaxRunDuration={MaxRunDuration}",
            opts.DefaultTenantStrategy, persistedTenant.Id, persistedTenant.Identifier, opts.MaxRunDuration);

        var sw = Stopwatch.StartNew();
        SingleTenantUpgradeResult result;
        try
        {
            result = await migrator.RunAsync(persistedTenant, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                "PolarSharp single-tenant -> MT upgrade: aborted — exceeded MaxRunDuration={MaxRunDuration} " +
                "after {Elapsed}. Increase SingleTenantUpgrade:MaxRunDuration and re-deploy, or " +
                "run the upgrade out-of-band via `dotnet polar-mt upgrade`.",
                opts.MaxRunDuration, sw.Elapsed);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PolarSharp single-tenant -> MT upgrade: failed after {Elapsed}. " +
                "Host startup will be blocked to prevent serving traffic with partially-stamped data.",
                sw.Elapsed);
            throw;
        }

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"PolarSharp single-tenant -> MT upgrade: provider migrator '{migrator.GetType().FullName}' " +
                $"reported failure after {result.Duration}. Message: {result.Message ?? "(none)"}. " +
                "Host startup is blocked to prevent serving traffic with partially-stamped data.");
        }

        _logger.LogInformation(
            "PolarSharp single-tenant -> MT upgrade: complete. " +
            "AlreadyComplete={AlreadyComplete} RowsStamped={RowsStamped} Duration={Duration} " +
            "EntityTypeBreakdown={EntityTypeBreakdown}",
            result.AlreadyComplete,
            result.RowsStamped,
            result.Duration,
            string.Join(", ", result.RowsStampedByEntityType.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<PolarTenantInfo> ResolveDefaultTenantAsync(
        IServiceProvider scopedServices,
        SingleTenantUpgradeOptions opts,
        CancellationToken ct)
    {
        switch (opts.DefaultTenantStrategy)
        {
            case DefaultTenantStrategy.LiteralDefault:
                {
                    var id = Guid.NewGuid().ToString();
                    return new PolarTenantInfo
                    {
                        Id = id,
                        Identifier = opts.LiteralDefaultTenantSlug,
                        Name = opts.LiteralDefaultTenantName,
                    };
                }

            case DefaultTenantStrategy.FirstUserOrganization:
                throw new NotSupportedException(
                    "DefaultTenantStrategy.FirstUserOrganization requires PolarSharp.MultiTenant.Identity " +
                    "and is not implemented in Stage A of the single-tenant upgrade feature. " +
                    "Use DefaultTenantStrategy.LiteralDefault or DefaultTenantStrategy.HostSupplied for now.");

            case DefaultTenantStrategy.HostSupplied:
                {
                    var resolver = scopedServices.GetService<IDefaultTenantResolver>()
                        ?? throw new InvalidOperationException(
                            "DefaultTenantStrategy.HostSupplied requires an IDefaultTenantResolver " +
                            "implementation registered in DI. Register one via " +
                            "`services.AddScoped<IDefaultTenantResolver, YourResolver>()` before calling " +
                            "AddPolarSingleTenantUpgrade.");
                    var resolved = await resolver.ResolveAsync(ct).ConfigureAwait(false);
                    if (resolved is null)
                    {
                        throw new InvalidOperationException(
                            $"IDefaultTenantResolver '{resolver.GetType().FullName}' returned null. " +
                            "Implementations must return a fully-populated PolarTenantInfo.");
                    }
                    if (string.IsNullOrEmpty(resolved.Id) || string.IsNullOrEmpty(resolved.Identifier))
                    {
                        throw new InvalidOperationException(
                            $"IDefaultTenantResolver '{resolver.GetType().FullName}' returned a tenant " +
                            "with empty Id or Identifier. Both must be set.");
                    }
                    return resolved;
                }

            default:
                throw new InvalidOperationException(
                    $"Unknown DefaultTenantStrategy value '{opts.DefaultTenantStrategy}'. " +
                    "Valid values: LiteralDefault, FirstUserOrganization, HostSupplied.");
        }
    }
}
