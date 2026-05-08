using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.Webhooks.Serialization;

namespace PolarSharp.Webhooks.Reconciliation;

/// <summary>
/// Persists the reconciliation checkpoint to a local JSON file.
/// </summary>
/// <remarks>
/// Suitable for single-instance deployments only. For multi-instance or cloud deployments,
/// implement <see cref="IReconciliationCheckpointStore"/> backed by Redis, SQL, or another
/// distributed store and register it with <c>ReconciliationStorage.Custom</c>.
/// </remarks>
internal sealed class FileCheckpointStore(
    IOptions<PolarReconciliationOptions> options,
    ILogger<FileCheckpointStore> logger) : IReconciliationCheckpointStore
{
    /// <inheritdoc/>
    public async Task<DateTimeOffset?> GetCheckpointAsync(CancellationToken ct = default)
    {
        var path = options.Value.FilePath;
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize(json, WebhookJsonContext.Default.WebhookInternalCheckpointData);
            return data?.LastProcessed;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "PolarSharp reconciliation: could not read checkpoint file {Path}. Starting from scratch.", path);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetCheckpointAsync(DateTimeOffset checkpoint, CancellationToken ct = default)
    {
        var path = options.Value.FilePath;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(new WebhookInternalCheckpointData(checkpoint), WebhookJsonContext.Default.WebhookInternalCheckpointData);
            await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "PolarSharp reconciliation: could not write checkpoint file {Path}.", path);
        }
    }
}
