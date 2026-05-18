using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.Notifications;

/// <summary>
/// <see cref="IValidateOptions{TOptions}"/> implementation for <see cref="TenantNotificationOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// The validator is a strict no-op when <see cref="TenantNotificationOptions.Enabled"/> is
/// <see langword="false"/> — it returns <see cref="ValidateOptionsResult.Success"/> immediately
/// without inspecting any sub-fields. This guarantees hosts that install the package but
/// leave it disabled pay no validation cost and never see false-positive failures from
/// default/empty values in the sub-options classes.
/// </para>
/// <para>
/// When enabled, the validator enforces channel-specific requirements: at least one channel
/// must be active, the email From: address must be a syntactically valid email, the Twilio
/// From: number must be E.164, the webhook URL must be an absolute HTTPS URI, and every
/// template surface that an enabled channel will read must be non-empty.
/// </para>
/// <para>
/// Environment-variable-based credentials (SendGrid API key, Twilio Account SID / Auth Token,
/// webhook signing secret) are intentionally NOT failed when the env var is unset at startup.
/// Operators frequently supply secrets later via systemd <c>EnvironmentFile</c> drop-ins,
/// Docker secrets, AWS Secrets Manager init scripts, or equivalent indirection. The validator
/// logs a Warning per missing env var so the operational issue is visible without being
/// load-bearing.
/// </para>
/// </remarks>
internal sealed partial class TenantNotificationOptionsValidator : IValidateOptions<TenantNotificationOptions>
{
    private static readonly Regex E164Regex = E164RegexFactory();
    private static readonly Regex EmailRegex = EmailRegexFactory();

    private readonly ILogger<TenantNotificationOptionsValidator> _logger;

    /// <summary>Initializes a new <see cref="TenantNotificationOptionsValidator"/>.</summary>
    /// <param name="logger">Logger used for env-var warnings.</param>
    public TenantNotificationOptionsValidator(ILogger<TenantNotificationOptionsValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, TenantNotificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // CRITICAL: when the master toggle is off, never inspect sub-fields. Default-valued
        // sub-options classes would otherwise produce a flood of false-positive errors.
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (!options.EnabledChannels.Email
            && !options.EnabledChannels.Sms
            && !options.EnabledChannels.Webhook)
        {
            failures.Add(
                "At least one of EnabledChannels.Email, .Sms, or .Webhook must be true " +
                "when notifications are enabled.");
        }

        if (options.EnabledChannels.Email)
        {
            ValidateEmail(options.Email, failures);
        }

        if (options.EnabledChannels.Sms)
        {
            ValidateSms(options.Sms, failures);
        }

        if (options.EnabledChannels.Webhook)
        {
            ValidateWebhook(options.Webhook, failures);
        }

        ValidateTemplates(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private void ValidateEmail(EmailChannelOptions email, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(email.FromAddress))
        {
            failures.Add("Email.FromAddress is required when the email channel is enabled.");
        }
        else if (!EmailRegex.IsMatch(email.FromAddress))
        {
            failures.Add($"Email.FromAddress '{email.FromAddress}' is not a syntactically valid email address.");
        }

        if (string.IsNullOrWhiteSpace(email.FromDisplayName))
        {
            failures.Add("Email.FromDisplayName is required when the email channel is enabled.");
        }

        if (email.Provider == EmailProvider.SendGrid)
        {
            if (string.IsNullOrWhiteSpace(email.SendGrid.ApiKeyEnvVar))
            {
                failures.Add("Email.SendGrid.ApiKeyEnvVar is required when the SendGrid email provider is selected.");
            }
            else
            {
                WarnIfEnvVarUnset(email.SendGrid.ApiKeyEnvVar);
            }
        }
    }

    private void ValidateSms(SmsChannelOptions sms, List<string> failures)
    {
        if (sms.Provider == SmsProvider.Twilio)
        {
            if (string.IsNullOrWhiteSpace(sms.Twilio.AccountSidEnvVar))
            {
                failures.Add("Sms.Twilio.AccountSidEnvVar is required when the Twilio SMS provider is selected.");
            }
            else
            {
                WarnIfEnvVarUnset(sms.Twilio.AccountSidEnvVar);
            }

            if (string.IsNullOrWhiteSpace(sms.Twilio.AuthTokenEnvVar))
            {
                failures.Add("Sms.Twilio.AuthTokenEnvVar is required when the Twilio SMS provider is selected.");
            }
            else
            {
                WarnIfEnvVarUnset(sms.Twilio.AuthTokenEnvVar);
            }

            if (string.IsNullOrWhiteSpace(sms.Twilio.FromNumber))
            {
                failures.Add("Sms.Twilio.FromNumber is required when the Twilio SMS provider is selected.");
            }
            else if (!E164Regex.IsMatch(sms.Twilio.FromNumber))
            {
                failures.Add(
                    $"Sms.Twilio.FromNumber '{sms.Twilio.FromNumber}' is not in E.164 format " +
                    "(e.g., '+15558675309').");
            }
        }
    }

    private void ValidateWebhook(WebhookChannelOptions webhook, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(webhook.Url))
        {
            failures.Add("Webhook.Url is required when the webhook channel is enabled.");
        }
        else if (!Uri.TryCreate(webhook.Url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"Webhook.Url '{webhook.Url}' must be an absolute https:// URI.");
        }

        if (webhook.TimeoutSeconds is < 1 or > 300)
        {
            failures.Add($"Webhook.TimeoutSeconds '{webhook.TimeoutSeconds}' must be in the range [1, 300].");
        }

        if (string.IsNullOrWhiteSpace(webhook.SigningSecretEnvVar))
        {
            failures.Add("Webhook.SigningSecretEnvVar is required when the webhook channel is enabled.");
        }
        else
        {
            WarnIfEnvVarUnset(webhook.SigningSecretEnvVar);
        }
    }

    private static void ValidateTemplates(TenantNotificationOptions options, List<string> failures)
    {
        var emailEnabled = options.EnabledChannels.Email;
        var smsEnabled = options.EnabledChannels.Sms;

        ValidateTemplate(options.Templates.Suspended, nameof(options.Templates.Suspended), emailEnabled, smsEnabled, failures);
        ValidateTemplate(options.Templates.Reactivated, nameof(options.Templates.Reactivated), emailEnabled, smsEnabled, failures);
        ValidateTemplate(options.Templates.Deactivated, nameof(options.Templates.Deactivated), emailEnabled, smsEnabled, failures);
        ValidateTemplate(options.Templates.Deleted, nameof(options.Templates.Deleted), emailEnabled, smsEnabled, failures);
    }

    private static void ValidateTemplate(
        NotificationTemplate template,
        string name,
        bool emailEnabled,
        bool smsEnabled,
        List<string> failures)
    {
        if (emailEnabled)
        {
            if (string.IsNullOrWhiteSpace(template.EmailSubject))
            {
                failures.Add($"Templates.{name}.EmailSubject is required when the email channel is enabled.");
            }
            if (string.IsNullOrWhiteSpace(template.EmailBody))
            {
                failures.Add($"Templates.{name}.EmailBody is required when the email channel is enabled.");
            }
        }

        if (smsEnabled && string.IsNullOrWhiteSpace(template.SmsBody))
        {
            failures.Add($"Templates.{name}.SmsBody is required when the SMS channel is enabled.");
        }
    }

    private void WarnIfEnvVarUnset(string envVarName)
    {
        if (string.IsNullOrEmpty(envVarName))
        {
            return;
        }

        var value = Environment.GetEnvironmentVariable(envVarName);
        if (string.IsNullOrEmpty(value))
        {
            _logger.LogWarning(
                "Tenant notification credential environment variable '{EnvVarName}' is not set " +
                "at startup. The credential may be supplied later (systemd EnvironmentFile, " +
                "Docker secrets, secrets manager). If it never arrives, the channel will fail " +
                "at delivery time.",
                envVarName);
        }
    }

    [GeneratedRegex(@"^\+[1-9]\d{1,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164RegexFactory();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegexFactory();
}
