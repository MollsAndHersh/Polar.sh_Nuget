using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.Notifications.Tests;

/// <summary>
/// Tests for <see cref="TenantStatusChangedNotificationHandler"/> — the MediatR bridge
/// from <see cref="TenantStatusChangedNotification"/> into <see cref="ITenantStatusNotifier"/>.
/// The handler must defensively swallow every non-cancellation, non-fatal exception so dispatch failures
/// never propagate back through MediatR into the originating <c>ITenantStatusService</c>.
/// </summary>
public sealed class TenantStatusChangedNotificationHandlerTests
{
    // --- Pass-through ------------------------------------------------------------------

    [Fact]
    public async Task Handle_invokes_ITenantStatusNotifier_with_the_notification()
    {
        var notifier = new RecordingNotifier();
        var sut = new TenantStatusChangedNotificationHandler(
            notifier,
            NullLogger<TenantStatusChangedNotificationHandler>.Instance);
        var notification = TestHelpers.Notification();

        await sut.Handle(notification, CancellationToken.None);

        var passed = Assert.Single(notifier.Calls);
        Assert.Same(notification, passed);
    }

    // --- Defensive exception swallowing ------------------------------------------------

    [Fact]
    public async Task Handle_swallows_NotificationDeliveryException_so_dispatch_failure_does_not_propagate()
    {
        var notifier = new RecordingNotifier
        {
            ThrowOnNotify = new TenantNotificationDeliveryException("SendGrid", 500, "boom"),
        };
        var log = new RecordingLogger<TenantStatusChangedNotificationHandler>();
        var sut = new TenantStatusChangedNotificationHandler(notifier, log);

        // Must NOT throw.
        await sut.Handle(TestHelpers.Notification(), CancellationToken.None);

        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Error &&
            e.Message.Contains("notification dispatcher", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_swallows_generic_Exception()
    {
        var notifier = new RecordingNotifier
        {
            ThrowOnNotify = new InvalidOperationException("unexpected programming error in dispatcher"),
        };
        var log = new RecordingLogger<TenantStatusChangedNotificationHandler>();
        var sut = new TenantStatusChangedNotificationHandler(notifier, log);

        // Must NOT throw.
        await sut.Handle(TestHelpers.Notification(), CancellationToken.None);

        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Error &&
            e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task Handle_propagates_OperationCanceledException_so_host_shutdown_completes_cleanly()
    {
        // Per standard async cancellation semantics, an OCE tied to the supplied token
        // MUST propagate so the host's graceful-shutdown path can observe the cancellation.
        // Swallowing OCE would (a) make the handler report success when work was actually
        // canceled, and (b) prevent IHostedService.StopAsync paths from completing their
        // observation of the cancellation chain. The catch-all in the handler intentionally
        // excludes OCE for this reason.
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var notifier = new RecordingNotifier
        {
            ThrowOnNotify = new OperationCanceledException(cts.Token),
        };
        var sut = new TenantStatusChangedNotificationHandler(
            notifier,
            NullLogger<TenantStatusChangedNotificationHandler>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.Handle(TestHelpers.Notification(), cts.Token));
    }

    // --- Test doubles ------------------------------------------------------------------

    private sealed class RecordingNotifier : ITenantStatusNotifier
    {
        public List<TenantStatusChangedNotification> Calls { get; } = new();
        public Exception? ThrowOnNotify { get; set; }

        public Task NotifyAsync(TenantStatusChangedNotification notification, CancellationToken ct)
        {
            Calls.Add(notification);
            if (ThrowOnNotify is not null) throw ThrowOnNotify;
            return Task.CompletedTask;
        }
    }
}
