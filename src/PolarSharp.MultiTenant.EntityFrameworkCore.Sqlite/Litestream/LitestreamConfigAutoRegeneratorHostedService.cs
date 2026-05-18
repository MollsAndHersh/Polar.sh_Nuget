using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

/// <summary>
/// Opt-in <see cref="IHostedService"/> that keeps a generated <c>litestream.yml</c> in lock-step
/// with the contents of the SQLite database directory.
/// </summary>
/// <remarks>
/// <para>
/// Activated when <see cref="LitestreamOptions.UseLitestream"/> AND
/// <see cref="LitestreamOptions.AutoRegenerateOnTenantChange"/> are both
/// <see langword="true"/>. With either flag off the service logs a single Info message and
/// exits — registration is always safe.
/// </para>
/// <para>
/// The service uses a <see cref="FileSystemWatcher"/> over the configured SQLite directory,
/// listening for <c>*.db</c> Created and Deleted events. Bursts of events (e.g., a bulk
/// tenant onboarding script) collapse into a single regeneration via a debounce window
/// configured by <see cref="LitestreamOptions.AutoRegenerateDebounceWindow"/>. On every
/// regeneration the service:
/// </para>
/// <list type="number">
///   <item>Calls <see cref="LitestreamConfigGenerator.Generate(string, LitestreamOptions)"/>
///   against the current directory contents.</item>
///   <item>Writes the resulting YAML atomically to
///   <see cref="LitestreamOptions.ConfigOutputPath"/> (temp file + <see cref="File.Move(string, string, bool)"/>).</item>
///   <item>On POSIX hosts, reads the PID from
///   <see cref="LitestreamOptions.LitestreamPidFilePath"/> and sends <c>SIGHUP</c> via a
///   direct <c>libc</c> P/Invoke, triggering Litestream's config-reload code path.</item>
///   <item>On Windows hosts, logs a Warning that signal-based reload is not supported —
///   the operator must restart Litestream manually for config changes to take effect.</item>
/// </list>
/// <para>
/// A single initial regeneration runs unconditionally at <see cref="StartAsync"/> so the
/// YAML reflects the on-disk state after a host restart (in case tenants were added or
/// removed while the host was down).
/// </para>
/// <para>
/// Stage C.1 covers <em>file</em> events only. Stage C.4 layers in MediatR
/// <c>TenantStatusChangedNotification</c> subscriptions so suspended tenants stop
/// replicating without their <c>.db</c> file being removed from disk; that work lives in a
/// separate stage and is not part of this service yet.
/// </para>
/// </remarks>
internal sealed class LitestreamConfigAutoRegeneratorHostedService : IHostedService, IDisposable
{
    private const int SIGHUP = 1;

    private readonly IOptionsMonitor<LitestreamOptions> _optionsMonitor;
    private readonly SqliteMasterDatabaseLocator _locator;
    private readonly LitestreamConfigGenerator _generator;
    private readonly ILogger<LitestreamConfigAutoRegeneratorHostedService> _logger;

    private FileSystemWatcher? _watcher;
    private Channel<RegenReason>? _channel;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    /// <summary>Initializes a new <see cref="LitestreamConfigAutoRegeneratorHostedService"/>.</summary>
    /// <param name="optionsMonitor">Resolved Litestream options. Read on each regen so live-reload of options is honored.</param>
    /// <param name="locator">Provides the resolved SQLite database directory the watcher observes.</param>
    /// <param name="generator">The generator that renders the YAML from the options + directory contents.</param>
    /// <param name="logger">Logger.</param>
    public LitestreamConfigAutoRegeneratorHostedService(
        IOptionsMonitor<LitestreamOptions> optionsMonitor,
        SqliteMasterDatabaseLocator locator,
        LitestreamConfigGenerator generator,
        ILogger<LitestreamConfigAutoRegeneratorHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(logger);

        _optionsMonitor = optionsMonitor;
        _locator = locator;
        _generator = generator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

        if (!options.UseLitestream || !options.AutoRegenerateOnTenantChange)
        {
            _logger.LogInformation(
                "Litestream auto-regenerator disabled (UseLitestream={UseLitestream}, " +
                "AutoRegenerateOnTenantChange={AutoRegenerate}). Hosted service exiting.",
                options.UseLitestream,
                options.AutoRegenerateOnTenantChange);
            return Task.CompletedTask;
        }

        var directory = _locator.DatabaseDirectory;
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            _logger.LogError(
                "Litestream auto-regenerator cannot start: database directory '{Directory}' " +
                "does not exist or is not readable. Service will not run.",
                directory);
            return Task.CompletedTask;
        }

        _channel = Channel.CreateUnbounded<RegenReason>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

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

        // Kick off an immediate sync so the YAML reflects on-disk state at startup.
        EnqueueRegen(new RegenReason(RegenTrigger.StartupInitialSync, null));

        _logger.LogInformation(
            "Litestream auto-regenerator started. Watching '{Directory}' for *.db Created/Deleted " +
            "events; debounce window {Debounce}; config output '{Output}'; PID file '{Pid}'.",
            directory,
            options.AutoRegenerateDebounceWindow,
            options.ConfigOutputPath,
            options.LitestreamPidFilePath);

        return Task.CompletedTask;
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

        _channel?.Writer.TryComplete();

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

    private void OnDbCreated(object sender, FileSystemEventArgs e)
        => EnqueueRegen(new RegenReason(RegenTrigger.FileCreated, e.Name));

    private void OnDbDeleted(object sender, FileSystemEventArgs e)
        => EnqueueRegen(new RegenReason(RegenTrigger.FileDeleted, e.Name));

    private void EnqueueRegen(RegenReason reason)
    {
        if (_channel is null)
        {
            return;
        }
        _channel.Writer.TryWrite(reason);
    }

    private async Task RegenLoopAsync(CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            return;
        }

        var reader = _channel.Reader;

        while (!cancellationToken.IsCancellationRequested)
        {
            RegenReason firstReason;
            try
            {
                firstReason = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
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
            // into the same regeneration. The "latest" reason wins for logging purposes.
            var debounce = _optionsMonitor.CurrentValue.AutoRegenerateDebounceWindow;
            var latestReason = firstReason;
            if (debounce > TimeSpan.Zero)
            {
                using var debounceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                debounceCts.CancelAfter(debounce);
                while (true)
                {
                    try
                    {
                        var next = await reader.ReadAsync(debounceCts.Token).ConfigureAwait(false);
                        latestReason = next;
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
                Regenerate(latestReason);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex,
                    "Litestream auto-regenerator failed to regenerate config (trigger: {Trigger}, file: {File}).",
                    latestReason.Trigger,
                    latestReason.FileName ?? "<none>");
            }
        }
    }

    private void Regenerate(RegenReason reason)
    {
        var options = _optionsMonitor.CurrentValue;
        var directory = _locator.DatabaseDirectory;

        var yaml = _generator.Generate(directory, options);
        WriteAtomic(options.ConfigOutputPath, yaml);

        var reasonDescription = reason.Trigger switch
        {
            RegenTrigger.FileCreated => string.Format(CultureInfo.InvariantCulture,
                "file Created: {0}", reason.FileName ?? "<unknown>"),
            RegenTrigger.FileDeleted => string.Format(CultureInfo.InvariantCulture,
                "file Deleted: {0}", reason.FileName ?? "<unknown>"),
            _ => "startup initial sync",
        };

        _logger.LogInformation(
            "Litestream auto-regenerator wrote '{Output}' ({Trigger}).",
            options.ConfigOutputPath,
            reasonDescription);

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

    private readonly record struct RegenReason(RegenTrigger Trigger, string? FileName);

    private enum RegenTrigger
    {
        StartupInitialSync = 0,
        FileCreated = 1,
        FileDeleted = 2,
    }

    private static class NativeMethods
    {
#pragma warning disable SYSLIB1054 // LibraryImportAttribute alternative not used to keep AOT-safe surface minimal
        [DllImport("libc", SetLastError = true)]
        internal static extern int kill(int pid, int sig);
#pragma warning restore SYSLIB1054
    }
}
