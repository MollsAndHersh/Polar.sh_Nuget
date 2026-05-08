using System.Diagnostics.Metrics;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using PolarSharp.Concurrency;

namespace PolarSharp.Tests.Concurrency;

/// <summary>
/// Property-based tests verifying the net-zero invariant of <see cref="InflightTracker"/>:
/// every request that starts must decrement the counter exactly once.
/// </summary>
public sealed class InflightTrackerPropertyTests
{
    /// <summary>
    /// After N requests complete normally, the net Add calls must sum to zero
    /// (one +1 and one -1 per request).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AfterAllRequestsComplete_NetDeltaIsZero(PositiveInt requestCount)
    {
        var count = Math.Min(requestCount.Get, 100);
        long netDelta = 0;

        using var meter = new Meter($"test-meter-{Guid.NewGuid()}");
        var gauge = meter.CreateUpDownCounter<long>("test.inflight");

        // Wire a MeterListener to capture the actual Add() deltas
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument == gauge)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
            Interlocked.Add(ref netDelta, value));
        listener.Start();

        var tracker = new InflightTracker(gauge);

        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(async () =>
        {
            using (tracker.TrackInflight("tenant-a", "orders"))
            {
                await Task.Yield(); // simulate async work
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // Force the listener to drain any buffered measurements
        listener.RecordObservableInstruments();

        var final = Volatile.Read(ref netDelta);
        return Prop.Label(
            Prop.ToProperty(final == 0),
            $"After {count} requests, net delta should be 0 but was {final}");
    }

    /// <summary>
    /// TrackInflight with different tenant/resource combos are independent — disposing one scope
    /// does not affect others.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MultipleScopes_DoNotInterfereWithEachOther(PositiveInt scopeCount)
    {
        var count = Math.Min(scopeCount.Get, 50);
        long netDelta = 0;

        using var meter = new Meter($"test-meter-{Guid.NewGuid()}");
        var gauge = meter.CreateUpDownCounter<long>("test.inflight");

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument == gauge)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
            Interlocked.Add(ref netDelta, value));
        listener.Start();

        var tracker = new InflightTracker(gauge);

        // Open all scopes, then close them all
        var scopes = Enumerable.Range(0, count)
            .Select(i => tracker.TrackInflight($"tenant-{i}", $"resource-{i}"))
            .ToArray();

        foreach (var scope in scopes)
            scope.Dispose();

        listener.RecordObservableInstruments();

        var final = Volatile.Read(ref netDelta);
        return Prop.Label(
            Prop.ToProperty(final == 0),
            $"After opening and closing {count} scopes, net delta should be 0 but was {final}");
    }

    /// <summary>
    /// Disposing a scope twice (double-dispose guard) must not produce an extra decrement.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property DoubleDispose_DoesNotProduceExtraDecrement(PositiveInt disposeCount)
    {
        var extraDisposes = Math.Min(disposeCount.Get, 20);
        long netDelta = 0;

        using var meter = new Meter($"test-meter-{Guid.NewGuid()}");
        var gauge = meter.CreateUpDownCounter<long>("test.inflight");

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument == gauge)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
            Interlocked.Add(ref netDelta, value));
        listener.Start();

        var tracker = new InflightTracker(gauge);
        var scope = tracker.TrackInflight("tenant-x", "products");

        // First dispose: legitimate
        scope.Dispose();

        // Extra disposes: the InflightScope guard must swallow these
        for (var i = 0; i < extraDisposes; i++)
            scope.Dispose();

        listener.RecordObservableInstruments();

        var final = Volatile.Read(ref netDelta);
        return Prop.Label(
            Prop.ToProperty(final == 0),
            $"After 1 open + {1 + extraDisposes} disposes, net delta should be 0 but was {final}");
    }
}
