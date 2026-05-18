using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.Notifications.Tests;

/// <summary>
/// Pure-function tests for <see cref="TenantNotificationOptionsValidator"/>. The validator
/// is internal; reachable here because the production assembly declares
/// <c>InternalsVisibleTo("PolarSharp.MultiTenant.Notifications.Tests")</c>.
/// </summary>
public sealed class TenantNotificationOptionsValidatorTests
{
    // --- Disabled-master-toggle short-circuit -----------------------------------------

    [Fact]
    public void Validate_returns_Success_immediately_when_Enabled_is_false()
    {
        var sut = NewValidator();
        // Even a maximally-broken sub-options graph (no channels enabled, empty templates,
        // empty env-var names) must NOT fail validation when Enabled is false — the
        // validator must short-circuit before inspecting anything.
        var opts = new TenantNotificationOptions
        {
            Enabled = false,
            EnabledChannels = new TenantNotificationChannels
            {
                Email = false, Sms = false, Webhook = false,
            },
            Email = new EmailChannelOptions { FromAddress = string.Empty, FromDisplayName = string.Empty },
            Sms = new SmsChannelOptions { Twilio = new TwilioOptions { AccountSidEnvVar = string.Empty, AuthTokenEnvVar = string.Empty, FromNumber = string.Empty } },
            Webhook = new WebhookChannelOptions { Url = string.Empty, SigningSecretEnvVar = string.Empty, TimeoutSeconds = -999 },
            Templates = new TenantNotificationTemplates
            {
                Suspended = new NotificationTemplate(),
                Reactivated = new NotificationTemplate(),
                Deactivated = new NotificationTemplate(),
                Deleted = new NotificationTemplate(),
            },
        };

        var result = sut.Validate(name: null, opts);

        Assert.True(result.Succeeded, $"Expected immediate success but got: {result.FailureMessage}");
    }

    // --- No-channels-enabled failure --------------------------------------------------

    [Fact]
    public void Validate_returns_Fail_when_Enabled_true_but_no_channel_enabled()
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels
        {
            Email = false, Sms = false, Webhook = false,
        };

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("At least one of EnabledChannels", result.FailureMessage!);
    }

    // --- Email channel branch ---------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.example")]
    public void Validate_returns_Fail_when_Email_enabled_but_FromAddress_invalid(string fromAddress)
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true };
        opts.Email.FromAddress = fromAddress;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("Email.FromAddress", result.FailureMessage!);
    }

    [Fact]
    public void Validate_returns_Fail_when_Email_enabled_but_FromDisplayName_empty()
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true };
        opts.Email.FromDisplayName = string.Empty;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains("Email.FromDisplayName", result.FailureMessage!);
    }

    [Fact]
    public void Validate_returns_Fail_when_Email_enabled_but_SendGrid_ApiKeyEnvVar_empty()
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true };
        opts.Email.SendGrid.ApiKeyEnvVar = string.Empty;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains("Email.SendGrid.ApiKeyEnvVar", result.FailureMessage!);
    }

    // --- SMS channel branch -----------------------------------------------------------

    [Theory]
    [InlineData("", "AUTH_TOKEN_VAR")]
    [InlineData("ACCOUNT_SID_VAR", "")]
    [InlineData("", "")]
    public void Validate_returns_Fail_when_Sms_enabled_but_Twilio_AccountSidEnvVar_or_AuthTokenEnvVar_empty(
        string sidEnvVar, string tokenEnvVar)
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true, Sms = true };
        opts.Sms.Twilio.AccountSidEnvVar = sidEnvVar;
        opts.Sms.Twilio.AuthTokenEnvVar = tokenEnvVar;
        opts.Sms.Twilio.FromNumber = "+15558675309";

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        if (string.IsNullOrEmpty(sidEnvVar))
        {
            Assert.Contains("Sms.Twilio.AccountSidEnvVar", result.FailureMessage!);
        }
        if (string.IsNullOrEmpty(tokenEnvVar))
        {
            Assert.Contains("Sms.Twilio.AuthTokenEnvVar", result.FailureMessage!);
        }
    }

    [Theory]
    [InlineData("")]                  // empty
    [InlineData("   ")]               // whitespace-only
    [InlineData("15558675309")]       // missing leading +
    [InlineData("+0155555")]          // leading zero after +
    [InlineData("+1-555-867-5309")]   // hyphens not allowed
    [InlineData("+1 555 867 5309")]   // spaces not allowed
    [InlineData("+15558675309abc")]   // trailing non-digits
    [InlineData("+1")]                // too short — E.164 requires 2-15 digits
    [InlineData("+1234567890123456")] // too long — > 15 digits
    public void Validate_returns_Fail_when_Sms_enabled_but_Twilio_FromNumber_invalid(string fromNumber)
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true, Sms = true };
        opts.Sms.Twilio.FromNumber = fromNumber;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains("Sms.Twilio.FromNumber", result.FailureMessage!);
    }

    [Theory]
    [InlineData("+15558675309")]     // US E.164
    [InlineData("+447911123456")]    // UK E.164
    [InlineData("+33145678901")]     // FR E.164
    [InlineData("+12")]              // minimum valid length (country code + 1 digit)
    [InlineData("+123456789012345")] // maximum valid length (15 digits total)
    public void Validate_returns_Success_for_Twilio_FromNumber_valid_E164(string fromNumber)
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true, Sms = true };
        opts.Sms.Twilio.FromNumber = fromNumber;

        var result = sut.Validate(name: null, opts);

        Assert.True(result.Succeeded, $"Expected success but got: {result.FailureMessage}");
    }

    // --- Webhook channel branch -------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://insecure.example.com/webhook")] // not https
    [InlineData("ftp://wrong-scheme.example.com")]
    [InlineData("/relative/path")]                       // not absolute
    [InlineData("not-a-url-at-all")]
    public void Validate_returns_Fail_when_Webhook_enabled_but_Url_invalid(string url)
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true, Webhook = true };
        opts.Webhook.Url = url;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains("Webhook.Url", result.FailureMessage!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-300)]
    [InlineData(301)]
    [InlineData(int.MaxValue)]
    public void Validate_returns_Fail_when_Webhook_enabled_but_TimeoutSeconds_out_of_range(int timeoutSeconds)
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true, Webhook = true };
        opts.Webhook.TimeoutSeconds = timeoutSeconds;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains("Webhook.TimeoutSeconds", result.FailureMessage!);
    }

    [Fact]
    public void Validate_returns_Fail_when_Webhook_enabled_but_SigningSecretEnvVar_empty()
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true, Webhook = true };
        opts.Webhook.SigningSecretEnvVar = string.Empty;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains("Webhook.SigningSecretEnvVar", result.FailureMessage!);
    }

    // --- Template validation ----------------------------------------------------------

    [Theory]
    [InlineData(nameof(TenantNotificationTemplates.Suspended))]
    [InlineData(nameof(TenantNotificationTemplates.Reactivated))]
    [InlineData(nameof(TenantNotificationTemplates.Deactivated))]
    [InlineData(nameof(TenantNotificationTemplates.Deleted))]
    public void Validate_returns_Fail_when_Email_enabled_but_template_EmailSubject_empty(string templateName)
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true };
        GetTemplate(opts.Templates, templateName).EmailSubject = string.Empty;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains($"Templates.{templateName}.EmailSubject", result.FailureMessage!);
    }

    [Theory]
    [InlineData(nameof(TenantNotificationTemplates.Suspended))]
    [InlineData(nameof(TenantNotificationTemplates.Reactivated))]
    [InlineData(nameof(TenantNotificationTemplates.Deactivated))]
    [InlineData(nameof(TenantNotificationTemplates.Deleted))]
    public void Validate_returns_Fail_when_Email_enabled_but_template_EmailBody_empty(string templateName)
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true };
        GetTemplate(opts.Templates, templateName).EmailBody = string.Empty;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains($"Templates.{templateName}.EmailBody", result.FailureMessage!);
    }

    [Theory]
    [InlineData(nameof(TenantNotificationTemplates.Suspended))]
    [InlineData(nameof(TenantNotificationTemplates.Reactivated))]
    [InlineData(nameof(TenantNotificationTemplates.Deactivated))]
    [InlineData(nameof(TenantNotificationTemplates.Deleted))]
    public void Validate_returns_Fail_when_Sms_enabled_but_template_SmsBody_empty(string templateName)
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true, Sms = true };
        opts.Sms.Twilio.FromNumber = "+15558675309";
        GetTemplate(opts.Templates, templateName).SmsBody = string.Empty;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains($"Templates.{templateName}.SmsBody", result.FailureMessage!);
    }

    // --- Multi-failure aggregation ----------------------------------------------------

    [Fact]
    public void Validate_aggregates_multiple_failures_into_single_Fail_result()
    {
        var sut = NewValidator();
        var opts = ValidOptions();
        opts.EnabledChannels = new TenantNotificationChannels { Email = true, Sms = true, Webhook = true };
        // Break several things at once.
        opts.Email.FromAddress = "not-an-email";
        opts.Email.FromDisplayName = string.Empty;
        opts.Sms.Twilio.FromNumber = "bogus";
        opts.Webhook.Url = "http://insecure.example.com";
        opts.Webhook.TimeoutSeconds = 999;
        opts.Templates.Suspended.EmailSubject = string.Empty;

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        var failures = result.Failures!.ToArray();
        Assert.True(failures.Length >= 5, $"Expected aggregated failures, got {failures.Length}: {string.Join(" | ", failures)}");
    }

    // --- Implements IValidateOptions --------------------------------------------------

    [Fact]
    public void Validator_implements_IValidateOptions_for_TenantNotificationOptions()
    {
        Assert.IsAssignableFrom<IValidateOptions<TenantNotificationOptions>>(NewValidator());
    }

    // --- helpers ----------------------------------------------------------------------

    private static TenantNotificationOptionsValidator NewValidator() =>
        new(NullLogger<TenantNotificationOptionsValidator>.Instance);

    /// <summary>
    /// Builds a fully-valid options instance with Enabled=true and the email channel on.
    /// Tests mutate one field at a time to exercise specific failure branches.
    /// </summary>
    private static TenantNotificationOptions ValidOptions() => new()
    {
        Enabled = true,
        EnabledChannels = new TenantNotificationChannels
        {
            Email = true, Sms = false, Webhook = false,
        },
        Email = new EmailChannelOptions
        {
            FromAddress = "platform@example.com",
            FromDisplayName = "PolarSharp Platform",
            SendGrid = new SendGridOptions { ApiKeyEnvVar = "SENDGRID_API_KEY" },
        },
        Sms = new SmsChannelOptions
        {
            Twilio = new TwilioOptions
            {
                AccountSidEnvVar = "TWILIO_ACCOUNT_SID",
                AuthTokenEnvVar = "TWILIO_AUTH_TOKEN",
                FromNumber = "+15558675309",
            },
        },
        Webhook = new WebhookChannelOptions
        {
            Url = "https://webhooks.example.com/polar-events",
            SigningSecretEnvVar = "POLARSHARP_WEBHOOK_SECRET",
            TimeoutSeconds = 10,
        },
    };

    private static NotificationTemplate GetTemplate(TenantNotificationTemplates templates, string name) => name switch
    {
        nameof(TenantNotificationTemplates.Suspended) => templates.Suspended,
        nameof(TenantNotificationTemplates.Reactivated) => templates.Reactivated,
        nameof(TenantNotificationTemplates.Deactivated) => templates.Deactivated,
        nameof(TenantNotificationTemplates.Deleted) => templates.Deleted,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown template name"),
    };
}
