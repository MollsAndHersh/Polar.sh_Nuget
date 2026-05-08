using Microsoft.Extensions.Hosting;
using PolarSharp.Telemetry;

namespace PolarSharp.Webhooks.Toast;

/// <summary>
/// Hosted service that wires <see cref="IPolarToastChannel"/> lifetime to the host lifecycle:
/// completes the channel writer on application stop so <c>ReadAllAsync</c> consumers exit
/// cleanly, and registers the channel depth gauge with <see cref="PolarMeter"/>.
/// </summary>
internal sealed class PolarToastChannelLifetime(
    IPolarToastChannel channel,
    PolarMeter meter,
    IHostApplicationLifetime lifetime) : IHostedService
{
    private IDisposable? _stoppingRegistration;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Register channel depth provider so polar.channel.depth gauge is populated.
        meter.RegisterChannelDepthProvider("toast", () => channel.Reader.Count);

        // Complete the writer when the host starts stopping.
        _stoppingRegistration = lifetime.ApplicationStopping.Register(
            static state => ((IPolarToastChannel)state!).Writer.TryComplete(),
            channel);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingRegistration?.Dispose();
        // Ensure the writer is complete even if StopAsync runs before ApplicationStopping fires.
        channel.Writer.TryComplete();
        return Task.CompletedTask;
    }
}
