using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PolarSharp.Webhooks.Dedup;

/// <summary>
/// An optional, bounded in-memory deduplication store for Polar webhook event IDs.
/// Prevents double-processing of at-least-once webhook deliveries for hosts that
/// do not have access to a distributed cache.
/// </summary>
/// <remarks>
/// Use a distributed cache (Redis, SQL) for production deployments with multiple instances.
/// This store is single-process only — shared state across replicas is not supported.
/// </remarks>
internal sealed class WebhookInMemoryDedupService : IWebhookIdempotencyStore, IHostedService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly PolarWebhookInMemoryDedupOptions _options;
    private readonly ILogger<WebhookInMemoryDedupService> _logger;
    private Timer? _pruneTimer;

    /// <summary>
    /// Initializes the dedup service.
    /// </summary>
    public WebhookInMemoryDedupService(
        PolarWebhookInMemoryDedupOptions options,
        ILogger<WebhookInMemoryDedupService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ValueTask<bool> HasBeenProcessedAsync(string webhookId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(webhookId);
        return ValueTask.FromResult(_seen.ContainsKey(webhookId));
    }

    /// <inheritdoc/>
    public ValueTask MarkProcessedAsync(string webhookId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(webhookId);
        _seen.TryAdd(webhookId, DateTimeOffset.UtcNow);

        // Enforce max-entries cap: drop oldest if over limit
        if (_seen.Count > _options.MaxEntries)
        {
            var oldest = _seen.OrderBy(kv => kv.Value).FirstOrDefault();
            if (oldest.Key is not null)
                _seen.TryRemove(oldest.Key, out _);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _pruneTimer = new Timer(Prune, null, _options.RetentionPeriod, _options.RetentionPeriod);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_pruneTimer is not null)
            await _pruneTimer.DisposeAsync().ConfigureAwait(false);
    }

    private void Prune(object? state)
    {
        var cutoff = DateTimeOffset.UtcNow - _options.RetentionPeriod;
        var expired = _seen.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
        foreach (var key in expired)
            _seen.TryRemove(key, out DateTimeOffset _);

        if (expired.Count > 0)
            _logger.LogDebug("PolarSharp WebhookDedup: pruned {Count} expired entries.", expired.Count);
    }
}
