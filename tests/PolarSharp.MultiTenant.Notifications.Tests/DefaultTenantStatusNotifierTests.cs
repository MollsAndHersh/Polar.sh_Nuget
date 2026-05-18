using Microsoft.Extensions.Logging;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.Notifications.Tests;

/// <summary>
/// Tests for <see cref="DefaultTenantStatusNotifier"/> — the dispatcher that resolves the
/// template for the lifecycle transition, renders placeholders, and fans out to enabled
/// channels in parallel. Channel failures are isolated; the dispatcher itself never throws.
/// </summary>
public sealed class DefaultTenantStatusNotifierTests
{
    // --- Disabled-toggle short-circuit -------------------------------------------------

    [Fact]
    public async Task NotifyAsync_does_nothing_when_Options_Enabled_is_false()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Enabled = false;
        var (email, sms, webhook, log) = (new RecordingEmailChannel(), new RecordingSmsChannel(), new RecordingWebhookChannel(), new RecordingLogger<DefaultTenantStatusNotifier>());
        var sut = NewSut(opts, email, sms, webhook, log);

        await sut.NotifyAsync(TestHelpers.Notification(), CancellationToken.None);

        Assert.Empty(email.Calls);
        Assert.Empty(sms.Calls);
        Assert.Empty(webhook.Calls);
    }

    // --- Template resolution per transition --------------------------------------------

    [Fact]
    public async Task NotifyAsync_resolves_Suspended_template_for_Active_to_Suspended_transition()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Templates.Suspended.EmailSubject = "SUSPENDED_MARKER {TenantName}";
        var email = new RecordingEmailChannel();
        var sut = NewSut(opts, email);

        await sut.NotifyAsync(TestHelpers.Notification(TenantStatus.Active, TenantStatus.Suspended), CancellationToken.None);

        var call = Assert.Single(email.Calls);
        Assert.StartsWith("SUSPENDED_MARKER ", call.Rendered.EmailSubject);
    }

    [Theory]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Inactive)]
    public async Task NotifyAsync_resolves_Reactivated_template_for_transition_to_Active(TenantStatus from)
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Templates.Reactivated.EmailSubject = "REACTIVATED_MARKER {TenantName}";
        var email = new RecordingEmailChannel();
        var sut = NewSut(opts, email);

        await sut.NotifyAsync(TestHelpers.Notification(from, TenantStatus.Active), CancellationToken.None);

        var call = Assert.Single(email.Calls);
        Assert.StartsWith("REACTIVATED_MARKER ", call.Rendered.EmailSubject);
    }

    [Fact]
    public async Task NotifyAsync_resolves_Deactivated_template_for_Active_to_Inactive_transition()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Templates.Deactivated.EmailSubject = "DEACTIVATED_MARKER {TenantName}";
        var email = new RecordingEmailChannel();
        var sut = NewSut(opts, email);

        await sut.NotifyAsync(TestHelpers.Notification(TenantStatus.Active, TenantStatus.Inactive), CancellationToken.None);

        var call = Assert.Single(email.Calls);
        Assert.StartsWith("DEACTIVATED_MARKER ", call.Rendered.EmailSubject);
    }

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Inactive)]
    public async Task NotifyAsync_resolves_Deleted_template_for_any_to_Deleted_transition(TenantStatus from)
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Templates.Deleted.EmailSubject = "DELETED_MARKER {TenantName}";
        var email = new RecordingEmailChannel();
        var sut = NewSut(opts, email);

        await sut.NotifyAsync(TestHelpers.Notification(from, TenantStatus.Deleted), CancellationToken.None);

        var call = Assert.Single(email.Calls);
        Assert.StartsWith("DELETED_MARKER ", call.Rendered.EmailSubject);
    }

    [Fact]
    public async Task NotifyAsync_logs_Debug_and_returns_for_unhandled_transition()
    {
        // Suspended -> Inactive has no template configured.
        var opts = TestHelpers.FullyEnabledOptions();
        var (email, sms, webhook, log) = (new RecordingEmailChannel(), new RecordingSmsChannel(), new RecordingWebhookChannel(), new RecordingLogger<DefaultTenantStatusNotifier>());
        var sut = NewSut(opts, email, sms, webhook, log);

        await sut.NotifyAsync(TestHelpers.Notification(TenantStatus.Suspended, TenantStatus.Inactive), CancellationToken.None);

        Assert.Empty(email.Calls);
        Assert.Empty(sms.Calls);
        Assert.Empty(webhook.Calls);
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Debug &&
            e.Message.Contains("No template", StringComparison.OrdinalIgnoreCase));
    }

    // --- Placeholder substitution ------------------------------------------------------

    [Fact]
    public async Task NotifyAsync_substitutes_template_placeholders_correctly()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Templates.Suspended = new NotificationTemplate
        {
            EmailSubject = "{TenantName} -> {NewStatus}",
            EmailBody = "Tenant {TenantIdentifier} changed from {PreviousStatus} to {NewStatus} at {OccurredAt}. Reason: {Reason}.",
            SmsBody = "{TenantName}: now {NewStatus} (was {PreviousStatus})",
        };
        var email = new RecordingEmailChannel();
        var sms = new RecordingSmsChannel();
        var sut = NewSut(opts, email, sms);

        var notification = TestHelpers.Notification(
            previous: TenantStatus.Active,
            next: TenantStatus.Suspended,
            reason: "Billing dispute",
            tenantName: "ACME Corporation",
            tenantIdentifier: "acme-corp",
            occurredAt: new DateTimeOffset(2026, 5, 18, 12, 30, 45, TimeSpan.Zero));

        await sut.NotifyAsync(notification, CancellationToken.None);

        var emailCall = Assert.Single(email.Calls);
        Assert.Equal("ACME Corporation -> Suspended", emailCall.Rendered.EmailSubject);
        Assert.Contains("Tenant acme-corp changed from Active to Suspended at", emailCall.Rendered.EmailBody);
        Assert.Contains("Reason: Billing dispute.", emailCall.Rendered.EmailBody);
        // "u" format: 2026-05-18 12:30:45Z
        Assert.Contains("2026-05-18 12:30:45Z", emailCall.Rendered.EmailBody);

        var smsCall = Assert.Single(sms.Calls);
        Assert.Equal("ACME Corporation: now Suspended (was Active)", smsCall.Rendered.SmsBody);
    }

    [Fact]
    public async Task NotifyAsync_falls_back_to_TenantIdentifier_when_TenantName_is_null()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.Templates.Suspended = new NotificationTemplate
        {
            EmailSubject = "Name={TenantName}",
            EmailBody = "Body",
            SmsBody = "Sms",
        };
        var email = new RecordingEmailChannel();
        var sut = NewSut(opts, email);

        await sut.NotifyAsync(
            TestHelpers.Notification(tenantName: null, tenantIdentifier: "fallback-id"),
            CancellationToken.None);

        var call = Assert.Single(email.Calls);
        Assert.Equal("Name=fallback-id", call.Rendered.EmailSubject);
    }

    // --- Email channel gating ----------------------------------------------------------

    [Fact]
    public async Task NotifyAsync_dispatches_to_Email_channel_when_enabled_and_email_verified()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true };
        var email = new RecordingEmailChannel();
        var sut = NewSut(opts, email);

        await sut.NotifyAsync(
            TestHelpers.Notification(siteManagerEmail: "verified@example.com", siteManagerEmailVerified: true),
            CancellationToken.None);

        var call = Assert.Single(email.Calls);
        Assert.Equal("verified@example.com", call.ToAddress);
    }

    [Fact]
    public async Task NotifyAsync_skips_Email_channel_when_SiteManagerEmailVerified_is_false()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true };
        opts.SendToUnverifiedEmail = false;
        var (email, log) = (new RecordingEmailChannel(), new RecordingLogger<DefaultTenantStatusNotifier>());
        var sut = NewSut(opts, email, log: log);

        await sut.NotifyAsync(
            TestHelpers.Notification(siteManagerEmail: "unverified@example.com", siteManagerEmailVerified: false),
            CancellationToken.None);

        Assert.Empty(email.Calls);
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("unverified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NotifyAsync_dispatches_to_Email_when_unverified_but_SendToUnverifiedEmail_is_true()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true };
        opts.SendToUnverifiedEmail = true;
        var email = new RecordingEmailChannel();
        var sut = NewSut(opts, email);

        await sut.NotifyAsync(
            TestHelpers.Notification(siteManagerEmail: "unverified@example.com", siteManagerEmailVerified: false),
            CancellationToken.None);

        var call = Assert.Single(email.Calls);
        Assert.Equal("unverified@example.com", call.ToAddress);
    }

    [Fact]
    public async Task NotifyAsync_skips_Email_when_SiteManagerEmail_is_empty()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true };
        var (email, log) = (new RecordingEmailChannel(), new RecordingLogger<DefaultTenantStatusNotifier>());
        var sut = NewSut(opts, email, log: log);

        await sut.NotifyAsync(
            TestHelpers.Notification(siteManagerEmail: string.Empty, siteManagerEmailVerified: true),
            CancellationToken.None);

        Assert.Empty(email.Calls);
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains("SiteManagerEmail is empty", StringComparison.OrdinalIgnoreCase));
    }

    // --- SMS channel gating ------------------------------------------------------------

    [Fact]
    public async Task NotifyAsync_dispatches_to_Sms_channel_when_enabled_and_phone_present()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Sms = true };
        var sms = new RecordingSmsChannel();
        var sut = NewSut(opts, sms: sms);

        await sut.NotifyAsync(
            TestHelpers.Notification(siteManagerPhone: "+15555550100"),
            CancellationToken.None);

        var call = Assert.Single(sms.Calls);
        Assert.Equal("+15555550100", call.ToNumber);
    }

    [Fact]
    public async Task NotifyAsync_skips_Sms_channel_when_phone_is_null()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Sms = true };
        var (sms, log) = (new RecordingSmsChannel(), new RecordingLogger<DefaultTenantStatusNotifier>());
        var sut = NewSut(opts, sms: sms, log: log);

        await sut.NotifyAsync(
            TestHelpers.Notification(siteManagerPhone: null),
            CancellationToken.None);

        Assert.Empty(sms.Calls);
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("no phone number", StringComparison.OrdinalIgnoreCase));
    }

    // --- Webhook channel ---------------------------------------------------------------

    [Fact]
    public async Task NotifyAsync_dispatches_to_Webhook_channel_when_enabled()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Webhook = true };
        var webhook = new RecordingWebhookChannel();
        var sut = NewSut(opts, webhook: webhook);

        await sut.NotifyAsync(TestHelpers.Notification(), CancellationToken.None);

        var call = Assert.Single(webhook.Calls);
        Assert.NotNull(call);
    }

    // --- Parallel fan-out --------------------------------------------------------------

    [Fact]
    public async Task NotifyAsync_fans_out_to_multiple_channels()
    {
        // We don't assert literal concurrency timing (flaky), but we DO assert that all
        // three channels received their call exactly once when all are enabled — which
        // is what the Task.WhenAll fan-out is responsible for delivering.
        var opts = TestHelpers.FullyEnabledOptions();
        var (email, sms, webhook) = (new RecordingEmailChannel(), new RecordingSmsChannel(), new RecordingWebhookChannel());
        var sut = NewSut(opts, email, sms, webhook);

        await sut.NotifyAsync(TestHelpers.Notification(), CancellationToken.None);

        Assert.Single(email.Calls);
        Assert.Single(sms.Calls);
        Assert.Single(webhook.Calls);
    }

    [Fact]
    public async Task NotifyAsync_invokes_all_channels_concurrently_via_Task_WhenAll()
    {
        // Concurrency probe: gate every channel on a TaskCompletionSource the test owns.
        // If the dispatcher sequenced the calls (await per-channel), the second channel
        // would never be entered until the first completed -- which it won't, because we
        // hold the gate. With concurrent fan-out, all three enter, then we release the
        // gate and they all complete.
        var opts = TestHelpers.FullyEnabledOptions();
        var gate = new TaskCompletionSource();
        var emailEntered = new TaskCompletionSource();
        var smsEntered = new TaskCompletionSource();
        var webhookEntered = new TaskCompletionSource();

        var email = new RecordingEmailChannel
        {
            BeforeReturn = async () => { emailEntered.TrySetResult(); await gate.Task; },
        };
        var sms = new RecordingSmsChannel
        {
            BeforeReturn = async () => { smsEntered.TrySetResult(); await gate.Task; },
        };
        var webhook = new RecordingWebhookChannel
        {
            BeforeReturn = async () => { webhookEntered.TrySetResult(); await gate.Task; },
        };
        var sut = NewSut(opts, email, sms, webhook);

        var dispatchTask = sut.NotifyAsync(TestHelpers.Notification(), CancellationToken.None);

        // All three channels must enter their gate before any completes -- proves fan-out.
        await Task.WhenAll(emailEntered.Task, smsEntered.Task, webhookEntered.Task)
            .WaitAsync(TimeSpan.FromSeconds(5));

        gate.SetResult();
        await dispatchTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(email.Calls);
        Assert.Single(sms.Calls);
        Assert.Single(webhook.Calls);
    }

    // --- Per-channel failure isolation -------------------------------------------------

    [Fact]
    public async Task NotifyAsync_isolates_per_channel_failures()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        var email = new RecordingEmailChannel
        {
            ThrowOnSend = new TenantNotificationDeliveryException("SendGrid", 500, "internal server error"),
        };
        var sms = new RecordingSmsChannel();
        var webhook = new RecordingWebhookChannel();
        var sut = NewSut(opts, email, sms, webhook);

        // Even though Email throws, the dispatcher must complete and SMS + Webhook must
        // still have been called.
        await sut.NotifyAsync(TestHelpers.Notification(), CancellationToken.None);

        Assert.Single(email.Calls);
        Assert.Single(sms.Calls);
        Assert.Single(webhook.Calls);
    }

    [Fact]
    public async Task NotifyAsync_logs_each_channel_failure_independently()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        var email = new RecordingEmailChannel
        {
            ThrowOnSend = new TenantNotificationDeliveryException("SendGrid", 500, "email exploded"),
        };
        var sms = new RecordingSmsChannel
        {
            ThrowOnSend = new TenantNotificationDeliveryException("Twilio", 502, "sms exploded"),
        };
        var webhook = new RecordingWebhookChannel(); // succeeds
        var log = new RecordingLogger<DefaultTenantStatusNotifier>();
        var sut = NewSut(opts, email, sms, webhook, log);

        await sut.NotifyAsync(TestHelpers.Notification(), CancellationToken.None);

        // Both failures are logged at Error.
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Email channel delivery failed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("SMS channel delivery failed", StringComparison.OrdinalIgnoreCase));
        // Successful webhook still ran.
        Assert.Single(webhook.Calls);
    }

    // --- helpers -----------------------------------------------------------------------

    private static DefaultTenantStatusNotifier NewSut(
        TenantNotificationOptions opts,
        RecordingEmailChannel? email = null,
        RecordingSmsChannel? sms = null,
        RecordingWebhookChannel? webhook = null,
        RecordingLogger<DefaultTenantStatusNotifier>? log = null)
    {
        return new DefaultTenantStatusNotifier(
            new StaticOptionsMonitor<TenantNotificationOptions>(opts),
            email ?? new RecordingEmailChannel(),
            sms ?? new RecordingSmsChannel(),
            webhook ?? new RecordingWebhookChannel(),
            (ILogger<DefaultTenantStatusNotifier>?)log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultTenantStatusNotifier>.Instance);
    }

    private static DefaultTenantStatusNotifier NewSut(
        TenantNotificationOptions opts,
        RecordingEmailChannel email,
        RecordingSmsChannel sms)
        => NewSut(opts, email, sms, null, null);

    private static DefaultTenantStatusNotifier NewSut(
        TenantNotificationOptions opts,
        RecordingEmailChannel email,
        RecordingLogger<DefaultTenantStatusNotifier> log)
        => NewSut(opts, email, null, null, log);
}
