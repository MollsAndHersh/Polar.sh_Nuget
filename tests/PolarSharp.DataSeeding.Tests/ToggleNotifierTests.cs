using System.Threading.Channels;
using PolarSharp.DataSeeding.Sync;

namespace PolarSharp.DataSeeding.Tests;

/// <summary>Verifies the toggle notifier writes to the channel and the sync service consumes events.</summary>
public sealed class ToggleNotifierTests
{
    [Fact]
    public void Notify_writes_to_underlying_channel()
    {
        var channel = Channel.CreateUnbounded<FakeDataToggleChanged>();
        var notifier = new ChannelFakeDataToggleNotifier(channel);

        var change = new FakeDataToggleChanged("tenant-x", NewValue: true, DateTimeOffset.UtcNow);
        Assert.True(notifier.Notify(change));

        Assert.True(channel.Reader.TryRead(out var dequeued));
        Assert.Equal("tenant-x", dequeued!.TenantId);
        Assert.True(dequeued.NewValue);
    }

    [Fact]
    public void Notify_returns_false_when_bounded_channel_with_default_wait_policy_is_full()
    {
        // Default Wait full-mode: TryWrite returns false when the channel is at capacity
        // (the producer should either back off or use the async WriteAsync).
        var channel = Channel.CreateBounded<FakeDataToggleChanged>(new BoundedChannelOptions(capacity: 1));
        var notifier = new ChannelFakeDataToggleNotifier(channel);

        Assert.True(notifier.Notify(new FakeDataToggleChanged("a", true, DateTimeOffset.UtcNow)));
        Assert.False(notifier.Notify(new FakeDataToggleChanged("b", true, DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void Notify_with_DropOldest_policy_always_accepts_and_evicts_old_entries()
    {
        // DropOldest: new writes always succeed; the oldest queued event is evicted to make
        // room. This is the policy the DI registration uses by default — favours fresh
        // toggle state over historical accuracy.
        var channel = Channel.CreateBounded<FakeDataToggleChanged>(new BoundedChannelOptions(capacity: 1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        var notifier = new ChannelFakeDataToggleNotifier(channel);

        Assert.True(notifier.Notify(new FakeDataToggleChanged("a", true, DateTimeOffset.UtcNow)));
        Assert.True(notifier.Notify(new FakeDataToggleChanged("b", true, DateTimeOffset.UtcNow)));
        Assert.True(channel.Reader.TryRead(out var survivor));
        Assert.Equal("b", survivor!.TenantId);
    }

    [Fact]
    public void Notify_with_null_throws()
    {
        var channel = Channel.CreateUnbounded<FakeDataToggleChanged>();
        var notifier = new ChannelFakeDataToggleNotifier(channel);
        Assert.Throws<ArgumentNullException>(() => notifier.Notify(null!));
    }
}
