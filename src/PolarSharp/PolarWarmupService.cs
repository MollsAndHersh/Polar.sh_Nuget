using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp;

/// <summary>
/// An optional <see cref="IHostedService"/> that issues a single benign API call during
/// startup to JIT-compile the full request pipeline before the application serves traffic.
/// </summary>
/// <remarks>
/// Only has an effect when <see cref="PolarOptions.WarmupOnStartup"/> is <see langword="true"/>
/// (default is <see langword="false"/> to keep startup fast). Has no effect in Native AOT builds
/// where JIT compilation does not occur.
/// </remarks>
internal sealed class PolarWarmupService(
    PolarClient client,
    IOptions<PolarOptions> options,
    ILogger<PolarWarmupService> logger) : IHostedService
{
    /// <summary>
    /// Issues a benign GET /v1/organizations?limit=1 call to warm up the HTTP pipeline.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.WarmupOnStartup)
            return;

        try
        {
            logger.LogInformation("PolarSharp: warming up HTTP pipeline (WarmupOnStartup=true).");
            await client.Organizations.EmptyPathSegment.GetAsync(q =>
            {
                q.QueryParameters.Limit = 1;
            }, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("PolarSharp: HTTP pipeline warm-up complete.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PolarSharp: warm-up call failed (non-fatal — continuing startup).");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
