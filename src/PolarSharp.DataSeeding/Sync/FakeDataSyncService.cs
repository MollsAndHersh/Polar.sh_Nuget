using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.DataSeeding.Sync;

/// <summary>
/// Domain event emitted when a tenant's <c>AllowFakeData</c> flag changes. Producers push
/// via <see cref="IFakeDataToggleNotifier"/>; the <see cref="FakeDataSyncService"/>
/// background service consumes via the registered <see cref="Channel{T}"/>.
/// </summary>
/// <param name="TenantId">The tenant whose flag changed.</param>
/// <param name="NewValue">The new value of <c>AllowFakeData</c>.</param>
/// <param name="OccurredAt">UTC of the change.</param>
public sealed record FakeDataToggleChanged(string TenantId, bool NewValue, DateTimeOffset OccurredAt);

/// <summary>
/// Host-facing API for raising <see cref="FakeDataToggleChanged"/>. Typically called from
/// <c>IPolarBusinessProfileService.SaveAsync</c> when the persisted profile's
/// <c>AllowFakeData</c> differs from the previous value.
/// </summary>
public interface IFakeDataToggleNotifier
{
    /// <summary>Enqueues a toggle-changed event. Returns <see langword="true"/> when accepted, <see langword="false"/> when the channel is full and the event was dropped.</summary>
    bool Notify(FakeDataToggleChanged change);
}

/// <summary>Default <see cref="IFakeDataToggleNotifier"/> backed by a bounded <see cref="Channel{T}"/>.</summary>
internal sealed class ChannelFakeDataToggleNotifier : IFakeDataToggleNotifier
{
    private readonly Channel<FakeDataToggleChanged> _channel;

    public ChannelFakeDataToggleNotifier(Channel<FakeDataToggleChanged> channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channel = channel;
    }

    public bool Notify(FakeDataToggleChanged change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return _channel.Writer.TryWrite(change);
    }
}

/// <summary>
/// Background service that consumes <see cref="FakeDataToggleChanged"/> events and reconciles
/// the tenant's fake-data state with Polar's sandbox.
/// </summary>
/// <remarks>
/// <para>
/// <strong>OFF → ON:</strong> publishes every <c>IsFakeData = true</c> local record to
/// Polar's sandbox so the merchant can exercise the full purchase flow against representative
/// data. (Wiring to <c>IPolarCatalogPublisher.PublishAsync</c> with
/// <c>PublishScope.AllFakeData</c> deferred — Polar HTTP impl is Phase 11.)
/// </para>
/// <para>
/// <strong>ON → OFF:</strong> archives every previously-published fake record in Polar
/// (PATCH <c>is_archived: true</c>) so the merchant's sandbox is clean. Local records stay —
/// the global query filter on <c>ITenantOwned</c> entities hides them automatically.
/// </para>
/// </remarks>
public sealed class FakeDataSyncService : BackgroundService
{
    private readonly Channel<FakeDataToggleChanged> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PolarDataSeedingOptions> _options;
    private readonly ILogger<FakeDataSyncService> _logger;

    /// <summary>Initializes the sync service.</summary>
    public FakeDataSyncService(
        Channel<FakeDataToggleChanged> channel,
        IServiceScopeFactory scopeFactory,
        IOptions<PolarDataSeedingOptions> options,
        ILogger<FakeDataSyncService> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _channel = channel;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var change in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                if (change.NewValue)
                {
                    _logger.LogInformation(
                        "FakeDataSyncService: tenant {TenantId} flipped AllowFakeData ON — publish-to-Polar deferred to Phase 11 (IPolarCatalogPublisher.PublishAsync with PublishScope.AllFakeData).",
                        change.TenantId);
                }
                else
                {
                    _logger.LogInformation(
                        "FakeDataSyncService: tenant {TenantId} flipped AllowFakeData OFF — Polar-archive deferred to Phase 11.",
                        change.TenantId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "FakeDataSyncService: failed to process toggle change for tenant {TenantId}",
                    change.TenantId);
            }
        }
    }
}
