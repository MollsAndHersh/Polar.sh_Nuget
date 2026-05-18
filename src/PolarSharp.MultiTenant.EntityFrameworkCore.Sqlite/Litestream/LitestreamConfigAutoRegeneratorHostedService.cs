using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

/// <summary>
/// Opt-in <see cref="IHostedService"/> that keeps a generated <c>litestream.yml</c> in lock-step
/// with the contents of the SQLite database directory AND the tenant lifecycle status.
/// </summary>
/// <remarks>
/// <para>
/// Activated when <see cref="LitestreamOptions.UseLitestream"/> AND
/// <see cref="LitestreamOptions.AutoRegenerateOnTenantChange"/> are both
/// <see langword="true"/>. With either flag off the service logs a single Info message and
/// exits — registration is always safe.
/// </para>
/// <para>
/// Regeneration is triggered from two sources, both fed through the shared
/// <see cref="LitestreamRegenCoordinator"/> channel:
/// </para>
/// <list type="number">
///   <item>
///   <b>File-system events.</b> A <see cref="FileSystemWatcher"/> over the configured SQLite
///   directory listens for <c>*.db</c> Created and Deleted events (e.g., new tenants
///   onboarded, tenants fully removed).
///   </item>
///   <item>
///   <b>Tenant lifecycle events.</b> <see cref="LitestreamTenantLifecycleHandler"/>
///   subscribes to MediatR <see cref="TenantStatusChangedNotification"/> events and
///   updates the coordinator's exclusion set when tenants are
///   Suspended/Inactive/Deleted or reactivated to Active.
///   </item>
/// </list>
/// <para>
/// Bursts of events (e.g., a bulk tenant-status batch update) collapse into a single
/// regeneration via a debounce window configured by
/// <see cref="LitestreamOptions.AutoRegenerateDebounceWindow"/>. On every regeneration the
/// service:
/// </para>
/// <list type="number">
///   <item>Reads the coordinator's current exclusion-set snapshot.</item>
///   <item>Calls <see cref="LitestreamConfigGenerator.Generate(string, LitestreamOptions, IReadOnlySet{Guid}?)"/>
///   against the current directory contents and exclusion set.</item>
///   <item>Writes the resulting YAML atomically to
///   <see cref="LitestreamOptions.ConfigOutputPath"/> (temp file + <see cref="File.Move(string, string, bool)"/>).</item>
///   <item>On POSIX hosts, reads the PID from
///   <see cref="LitestreamOptions.LitestreamPidFilePath"/> and sends <c>SIGHUP</c> via a
///   direct <c>libc</c> P/Invoke, triggering Litestream's config-reload code path.</item>
///   <item>On Windows hosts, logs a Warning that signal-based reload is not supported —
///   the operator must restart Litestream manually for config changes to take effect.</item>
/// </list>
/// <para>
/// At <see cref="StartAsync"/> the service:
/// </para>
/// <list type="bullet">
///   <item>Queries the <see cref="IMultiTenantStore{TTenantInfo}"/> for tenants whose
///   <see cref="PolarTenantInfo.Status"/> is not <see cref="TenantStatus.Active"/> and seeds
///   the coordinator's exclusion set, so a host restart after suspensions still produces
///   correct YAML on the first regen.</item>
///   <item>Enqueues a startup-initial-sync signal so the YAML reflects on-disk state and
///   seeded exclusions immediately.</item>
/// </list>
/// <para>
/// Per the Stage C design decision, existing cloud snapshots are PRESERVED when a tenant is
/// suspended (subject to <see cref="LitestreamOptions.RetentionDays"/>) — only new WAL
/// replication stops.
/// </para>
/// </remarks>
internal sealed class LitestreamConfigAutoRegeneratorHostedService : IHostedService, IDisposable
{
    private const int SIGHUP = 1;

    private readonly IOptionsMonitor<LitestreamOptions> _optionsMonitor;
    private readonly SqliteMasterDatabaseLocator _locator;
    private readonly LitestreamConfigGenerator _generator;
    private readonly LitestreamRegenCoordinator _coordinator;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LitestreamConfigAutoRegeneratorHostedService> _logger;

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    /// <summary>Initializes a new <see cref="LitestreamConfigAutoRegeneratorHostedService"/>.</summary>
    /// <param name="optionsMonitor">Resolved Litestream options. Read on each regen so live-reload of options is honored.</param>
    /// <param name="locator">Provides the resolved SQLite database directory the watcher observes.</param>
    /// <param name="generator">The generator that renders the YAML from the options + directory contents + exclusion set.</param>
    /// <param name="coordinator">The shared regen-coordinator holding the exclusion set and signal channel.</param>
    /// <param name="serviceProvider">Root service provider used to resolve a scoped <see cref="IMultiTenantStore{TTenantInfo}"/> for startup exclusion seeding.</param>
    /// <param name="logger">Logger.</param>
    public LitestreamConfigAutoRegeneratorHostedService(
        IOptionsMonitor<LitestreamOptions> optionsMonitor,
        SqliteMasterDatabaseLocator locator,
        LitestreamConfigGenerator generator,
        LitestreamRegenCoordinator coordinator,
        IServiceProvider serviceProvider,
        ILogger<LitestreamConfigAutoRegeneratorHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _optionsMonitor = optionsMonitor;
        _locator = locator;
        _generator = generator;
        _coordinator = coordinator;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

        if (!options.UseLitestream || !options.AutoRegenerateOnTenantChange)
        {
            _logger.LogInformation(
                "Litestream auto-regenerator disabled (UseLitestream={UseLitestream}, " +
                "AutoRegenerateOnTenantChange={AutoRegenerate}). Hosted service exiting.",
                options.UseLitestream,
                options.AutoRegenerateOnTenantChange);
            return;
        }

        var directory = _locator.DatabaseDirectory;
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            _logger.LogError(
                "Litestream auto-regenerator cannot start: database directory '{Directory}' " +
                "does not exist or is not readable. Service will not run.",
                directory);
            return;
        }

        await SeedExclusionsFromStoreAsync(cancellationToken).ConfigureAwait(false);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => RegenLoopAsync(_cts.Token), CancellationToken.None);

        _watcher = new FileSystemWatcher(directory, "*.db")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            IncludeSubdirectories = false,
        };
        _watcher.Created += OnDbCreated;
        _watcher.Deleted += OnDbDeleted;
        _watcher.EnableRaisingEvents = true;

        // Kick off an immediate sync so the YAML reflects on-disk state + seeded exclusions at startup.
        _coordinator.SignalRegen(new RegenSignal(RegenTrigger.StartupInitialSync, TenantId: null, Detail: null));

        _logger.LogInformation(
            "Litestream auto-regenerator started. Watching '{Directory}' for *.db Created/Deleted " +
            "events; subscribed to TenantStatusChangedNotification via coordinator; debounce window " +
            "{Debounce}; config output '{Output}'; PID file '{Pid}'.",
            directory,
            options.AutoRegenerateDebounceWindow,
            options.ConfigOutputPath,
            options.LitestreamPidFilePath);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnDbCreated;
            _watcher.Deleted -= OnDbDeleted;
        }

        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_loopTask is not null)
        {
            try
            {
                // VSTHRD003: the loop Task was created via Task.Run on the thread-pool, not on
                // this context. Awaiting it for graceful shutdown is intentional and safe — there
                // is no synchronization-context-bound continuation that could deadlock.
#pragma warning disable VSTHRD003
                await _loopTask.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _watcher?.Dispose();
        _cts?.Dispose();
    }

    private async Task SeedExclusionsFromStoreAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetService<IMultiTenantStore<PolarTenantInfo>>();
        if (store is null)
        {
            _logger.LogDebug(
                "Litestream auto-regenerator: IMultiTenantStore<PolarTenantInfo> not registered; " +
                "skipping startup exclusion seeding. Tenant lifecycle handler will populate the set on the first event.");
            return;
        }

        try
        {
            var all = await store.GetAllAsync().ConfigureAwait(false);
            var nonActive = all
                .Where(t => t.Status != TenantStatus.Active)
                .Select(t => t.TenantId)
                .Where(id => id != Guid.Empty)
                .ToArray();

            if (nonActive.Length > 0)
            {
                _coordinator.SeedExclusions(nonActive);
            }
            else
            {
                _logger.LogDebug(
                    "Litestream auto-regenerator: startup tenant-store query returned no non-Active tenants; " +
                    "exclusion set remains empty.");
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Best-effort seeding — log and continue. The lifecycle handler will repopulate on the next status change.
            _logger.LogWarning(ex,
                "Litestream auto-regenerator: startup exclusion seeding from tenant store failed; " +
                "continuing with empty exclusion set. Subsequent lifecycle events will repopulate.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void OnDbCreated(object sender, FileSystemEventArgs e)
        => _coordinator.SignalRegen(new RegenSignal(RegenTrigger.FileCreated, TenantId: null, Detail: e.Name));

    private void OnDbDeleted(object sender, FileSystemEventArgs e)
        => _coordinator.SignalRegen(new RegenSignal(RegenTrigger.FileDeleted, TenantId: null, Detail: e.Name));

    private async Task RegenLoopAsync(CancellationToken cancellationToken)
    {
        var reader = _coordinator.Reader;

        while (!cancellationToken.IsCancellationRequested)
        {
            RegenSignal firstSignal;
            try
            {
                firstSignal = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ChannelClosedException)
            {
                return;
            }

            // Debounce: collapse any further events that arrive within the debounce window
            // into the same regeneration. The "latest" signal wins for logging purposes.
            var debounce = _optionsMonitor.CurrentValue.AutoRegenerateDebounceWindow;
            var latestSignal = firstSignal;
            if (debounce > TimeSpan.Zero)
            {
                using var debounceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                debounceCts.CancelAfter(debounce);
                while (true)
                {
                    try
                    {
                        var next = await reader.ReadAsync(debounceCts.Token).ConfigureAwait(false);
                        latestSignal = next;
                    }
                    catch (OperationCanceledException) when (debounceCts.IsCancellationRequested
                        && !cancellationToken.IsCancellationRequested)
                    {
                        // Debounce window elapsed; proceed to regen.
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (ChannelClosedException)
                    {
                        break;
                    }
                }
            }

            try
            {
                Regenerate(latestSignal);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex,
                    "Litestream auto-regenerator failed to regenerate config (trigger: {Trigger}, tenant: {Tenant}, detail: {Detail}).",
                    latestSignal.Trigger,
                    latestSignal.TenantId?.ToString() ?? "<none>",
                    latestSignal.Detail ?? "<none>");
            }
        }
    }

    private void Regenerate(RegenSignal signal)
    {
        var options = _optionsMonitor.CurrentValue;
        var directory = _locator.DatabaseDirectory;
        var exclusions = _coordinator.GetCurrentExclusions();

        var yaml = _generator.Generate(directory, options, exclusions);
        WriteAtomic(options.ConfigOutputPath, yaml);

        var signalDescription = signal.Trigger switch
        {
            RegenTrigger.FileCreated => string.Format(CultureInfo.InvariantCulture,
                "file Created: {0}", signal.Detail ?? "<unknown>"),
            RegenTrigger.FileDeleted => string.Format(CultureInfo.InvariantCulture,
                "file Deleted: {0}", signal.Detail ?? "<unknown>"),
            RegenTrigger.TenantExcluded => string.Format(CultureInfo.InvariantCulture,
                "tenant {0} excluded ({1})", signal.TenantId, signal.Detail ?? "<no reason>"),
            RegenTrigger.TenantReincluded => string.Format(CultureInfo.InvariantCulture,
                "tenant {0} re-included", signal.TenantId),
            _ => "startup initial sync",
        };

        _logger.LogInformation(
            "Litestream auto-regenerator wrote '{Output}' ({Trigger}; {ExclusionCount} tenant(s) excluded).",
            options.ConfigOutputPath,
            signalDescription,
            exclusions.Count);

        SignalLitestream(options);
    }

    private static void WriteAtomic(string destinationPath, string content)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = destinationPath + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, destinationPath, overwrite: true);
    }

    private void SignalLitestream(LitestreamOptions options)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning(
                "Litestream auto-regenerator wrote a new config to '{Output}', but cannot " +
                "signal the Litestream process to reload on Windows (POSIX SIGHUP is not " +
                "natively supported). Restart Litestream manually to pick up the change.",
                options.ConfigOutputPath);
            return;
        }

        if (!File.Exists(options.LitestreamPidFilePath))
        {
            _logger.LogWarning(
                "Litestream PID file '{PidFile}' not found; cannot signal Litestream to reload " +
                "its config. Verify Litestream is running and configured to write its PID file.",
                options.LitestreamPidFilePath);
            return;
        }

        string pidContents;
        try
        {
            pidContents = File.ReadAllText(options.LitestreamPidFilePath).Trim();
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex,
                "Failed to read Litestream PID file '{PidFile}'; cannot signal reload.",
                options.LitestreamPidFilePath);
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex,
                "Permission denied reading Litestream PID file '{PidFile}'; cannot signal reload.",
                options.LitestreamPidFilePath);
            return;
        }

        if (!int.TryParse(pidContents, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)
            || pid <= 0)
        {
            _logger.LogWarning(
                "Litestream PID file '{PidFile}' did not contain a valid positive integer (got '{Contents}'); " +
                "cannot signal reload.",
                options.LitestreamPidFilePath,
                pidContents);
            return;
        }

        int result;
        try
        {
            result = NativeMethods.kill(pid, SIGHUP);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex,
                "Failed to invoke libc kill({Pid}, SIGHUP) to reload Litestream.",
                pid);
            return;
        }

        if (result != 0)
        {
            var errno = Marshal.GetLastWin32Error();
            _logger.LogWarning(
                "libc kill({Pid}, SIGHUP) returned {Result} (errno {Errno}); Litestream may not " +
                "have reloaded its config.",
                pid,
                result,
                errno);
            return;
        }

        _logger.LogInformation(
            "Sent SIGHUP to Litestream process (PID {Pid}) for config reload.", pid);
    }

    private static class NativeMethods
    {
#pragma warning disable SYSLIB1054 // LibraryImportAttribute alternative not used to keep AOT-safe surface minimal
        [DllImport("libc", SetLastError = true)]
        internal static extern int kill(int pid, int sig);
#pragma warning restore SYSLIB1054
    }
}
