using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;

namespace PolarSharp.Webhooks.Toast;

/// <summary>
/// Hosted service that wires <see cref="IPolarToastChannel"/> lifetime to the host lifecycle:
/// completes the channel writer on application stop so <c>ReadAllAsync</c> consumers exit
/// cleanly, and optionally registers the channel depth gauge when a <see cref="Meter"/>
/// is available via <see cref="IMeterFactory"/>.
/// </summary>
internal sealed class PolarToastChannelLifetime(
    IPolarToastChannel channel,
    IMeterFactory? meterFactory,
    IHostApplicationLifetime lifetime) : IHostedService
{
    private IDisposable? _stoppingRegistration;
    private Meter? _meter;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Register channel depth gauge when metrics are available (optional — not required
        // for standalone deployments that haven't configured OpenTelemetry).
        if (meterFactory is not null)
        {
            _meter = meterFactory.Create("PolarSharp.Webhooks");
            _meter.CreateObservableGauge(
                "polar.channel.depth",
                observeValue: () => channel.Reader.Count,
                description: "Number of unread toast notifications queued in the bounded channel.");
        }

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
        _meter?.Dispose();
        // Ensure the writer is complete even if StopAsync runs before ApplicationStopping fires.
        channel.Writer.TryComplete();
        return Task.CompletedTask;
    }
}
