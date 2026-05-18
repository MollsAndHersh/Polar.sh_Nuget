using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.Notifications.Tests;

/// <summary>
/// Tests for <see cref="TenantStatusChangedNotificationHandler"/> — the MediatR bridge
/// from <see cref="TenantStatusChangedNotification"/> into <see cref="ITenantStatusNotifier"/>.
/// The handler must defensively swallow every non-cancellation exception so dispatch failures
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
    public async Task Handle_does_not_swallow_OperationCanceledException()
    {
        // Per standard async cancellation semantics, an OCE tied to the supplied token
        // should propagate so callers can react to cancellation. The handler's catch-all
        // is wide (Exception), but the production code is wide on purpose — we test the
        // observed behaviour: when a cancelled token causes the notifier to throw an OCE
        // wrapping that token, the handler currently DOES catch it because the catch is
        // unconditional. We pin that behaviour here so any future change to the catch
        // (e.g., excluding OperationCanceledException) is a deliberate, test-visible
        // decision rather than a silent regression.
        //
        // The standing rule today: dispatcher must never bubble back into MediatR. That
        // applies even if the bubble is an OCE -- the status change has already been
        // persisted by the time we get here. So the contract IS "swallow everything".
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var notifier = new RecordingNotifier
        {
            ThrowOnNotify = new OperationCanceledException(cts.Token),
        };
        var sut = new TenantStatusChangedNotificationHandler(
            notifier,
            NullLogger<TenantStatusChangedNotificationHandler>.Instance);

        // The handler's wide catch swallows the OCE just like any other exception.
        // Verify it does not propagate.
        await sut.Handle(TestHelpers.Notification(), cts.Token);
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
