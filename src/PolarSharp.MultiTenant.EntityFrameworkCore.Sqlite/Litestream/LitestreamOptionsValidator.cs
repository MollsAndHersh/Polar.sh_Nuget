using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

/// <summary>
/// <see cref="IValidateOptions{TOptions}"/> implementation for <see cref="LitestreamOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// The validator is a strict no-op when <see cref="LitestreamOptions.UseLitestream"/> is
/// <see langword="false"/> — it returns <see cref="ValidateOptionsResult.Success"/> immediately
/// without inspecting any sub-fields. This guarantees that hosts which never set the toggle
/// pay no validation cost and never see false-positive failures from default/empty values in
/// the sub-options classes.
/// </para>
/// <para>
/// When the toggle is on, the validator enforces:
/// <list type="bullet">
///   <item>A valid <see cref="LitestreamReplicaTargetType"/> enum value.</item>
///   <item>That the sub-options object matching the chosen replica type has all required
///   fields populated and that any referenced files / directories exist or are creatable.</item>
///   <item>That every numeric tuning parameter (sync interval, snapshot interval, retention,
///   metrics port, health-check lag threshold) falls in its documented range.</item>
/// </list>
/// </para>
/// <para>
/// Environment-variable-based credentials (S3, Azure) are intentionally NOT failed when
/// the env var is unset at startup. Operators frequently supply secrets later via systemd
/// EnvironmentFile drop-ins, Docker secrets, AWS Secrets Manager init scripts, or
/// equivalent indirection. Instead the validator logs a Warning per missing env var so the
/// operational issue is visible without being load-bearing.
/// </para>
/// </remarks>
internal sealed class LitestreamOptionsValidator : IValidateOptions<LitestreamOptions>
{
    private readonly ILogger<LitestreamOptionsValidator> _logger;

    /// <summary>Initializes a new <see cref="LitestreamOptionsValidator"/>.</summary>
    /// <param name="logger">Logger used for env-var warnings.</param>
    public LitestreamOptionsValidator(ILogger<LitestreamOptionsValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, LitestreamOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // CRITICAL: when the master toggle is off, never inspect sub-fields. Default-valued
        // sub-options classes would otherwise produce a flood of false-positive errors.
        if (!options.UseLitestream)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (!Enum.IsDefined(options.ReplicaTargetType))
        {
            failures.Add(
                $"{nameof(LitestreamOptions.ReplicaTargetType)} '{options.ReplicaTargetType}' " +
                $"is not a valid {nameof(LitestreamReplicaTargetType)} value.");
        }
        else
        {
            ValidateReplicaTarget(options, failures);
        }

        ValidateRange(failures, nameof(LitestreamOptions.SyncIntervalSeconds),
            options.SyncIntervalSeconds, min: 1, max: 3600);
        ValidateRange(failures, nameof(LitestreamOptions.SnapshotIntervalMinutes),
            options.SnapshotIntervalMinutes, min: 1, max: 1440);
        ValidateRange(failures, nameof(LitestreamOptions.RetentionDays),
            options.RetentionDays, min: 1, max: 365);
        ValidateRange(failures, nameof(LitestreamOptions.MetricsPort),
            options.MetricsPort, min: 1024, max: 65535);
        ValidateRange(failures, nameof(LitestreamOptions.HealthCheckMaxLagSeconds),
            options.HealthCheckMaxLagSeconds, min: 1, max: 3600);

        if (options.AutoRegenerateOnTenantChange)
        {
            RequireNonEmpty(failures, nameof(LitestreamOptions.ConfigOutputPath), options.ConfigOutputPath);
            RequireNonEmpty(failures, nameof(LitestreamOptions.LitestreamPidFilePath), options.LitestreamPidFilePath);
            if (options.AutoRegenerateDebounceWindow <= TimeSpan.Zero)
            {
                failures.Add(
                    $"{nameof(LitestreamOptions.AutoRegenerateDebounceWindow)} " +
                    $"'{options.AutoRegenerateDebounceWindow}' must be greater than zero when " +
                    $"{nameof(LitestreamOptions.AutoRegenerateOnTenantChange)} is true.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private void ValidateReplicaTarget(LitestreamOptions options, List<string> failures)
    {
        switch (options.ReplicaTargetType)
        {
            case LitestreamReplicaTargetType.S3:
                ValidateS3(options.S3, failures);
                break;
            case LitestreamReplicaTargetType.AzureBlob:
                ValidateAzureBlob(options.AzureBlob, failures);
                break;
            case LitestreamReplicaTargetType.GoogleCloudStorage:
                ValidateGcs(options.GoogleCloudStorage, failures);
                break;
            case LitestreamReplicaTargetType.Sftp:
                ValidateSftp(options.Sftp, failures);
                break;
            case LitestreamReplicaTargetType.LocalDisk:
                ValidateLocalDisk(options.LocalDisk, failures);
                break;
        }
    }

    private void ValidateS3(LitestreamS3Options s3, List<string> failures)
    {
        RequireNonEmpty(failures, "S3.Bucket", s3.Bucket);
        RequireNonEmpty(failures, "S3.Region", s3.Region);
        RequireNonEmpty(failures, "S3.AccessKeyIdEnvVar", s3.AccessKeyIdEnvVar);
        RequireNonEmpty(failures, "S3.SecretAccessKeyEnvVar", s3.SecretAccessKeyEnvVar);

        if (!string.IsNullOrEmpty(s3.EndpointUrl) &&
            !Uri.TryCreate(s3.EndpointUrl, UriKind.Absolute, out _))
        {
            failures.Add(
                $"S3.EndpointUrl '{s3.EndpointUrl}' is not a valid absolute URI.");
        }

        WarnIfEnvVarUnset(s3.AccessKeyIdEnvVar);
        WarnIfEnvVarUnset(s3.SecretAccessKeyEnvVar);
    }

    private void ValidateAzureBlob(LitestreamAzureBlobOptions azure, List<string> failures)
    {
        RequireNonEmpty(failures, "AzureBlob.AccountName", azure.AccountName);
        RequireNonEmpty(failures, "AzureBlob.Container", azure.Container);
        RequireNonEmpty(failures, "AzureBlob.AccessKeyEnvVar", azure.AccessKeyEnvVar);

        WarnIfEnvVarUnset(azure.AccessKeyEnvVar);
    }

    private static void ValidateGcs(LitestreamGoogleCloudStorageOptions gcs, List<string> failures)
    {
        RequireNonEmpty(failures, "GoogleCloudStorage.Bucket", gcs.Bucket);
        RequireNonEmpty(failures, "GoogleCloudStorage.CredentialsJsonPath", gcs.CredentialsJsonPath);

        if (!string.IsNullOrEmpty(gcs.CredentialsJsonPath) && !File.Exists(gcs.CredentialsJsonPath))
        {
            failures.Add(
                $"GoogleCloudStorage.CredentialsJsonPath '{gcs.CredentialsJsonPath}' " +
                "does not point to an existing file.");
        }
    }

    private static void ValidateSftp(LitestreamSftpOptions sftp, List<string> failures)
    {
        RequireNonEmpty(failures, "Sftp.Host", sftp.Host);
        RequireNonEmpty(failures, "Sftp.User", sftp.User);
        RequireNonEmpty(failures, "Sftp.Path", sftp.Path);
        RequireNonEmpty(failures, "Sftp.PrivateKeyPath", sftp.PrivateKeyPath);

        if (sftp.Port is < 1 or > 65535)
        {
            failures.Add($"Sftp.Port '{sftp.Port}' must be in the range [1, 65535].");
        }

        if (!string.IsNullOrEmpty(sftp.PrivateKeyPath) && !File.Exists(sftp.PrivateKeyPath))
        {
            failures.Add(
                $"Sftp.PrivateKeyPath '{sftp.PrivateKeyPath}' does not point to an existing file.");
        }
    }

    private static void ValidateLocalDisk(LitestreamLocalDiskOptions local, List<string> failures)
    {
        RequireNonEmpty(failures, "LocalDisk.Path", local.Path);

        if (string.IsNullOrEmpty(local.Path))
        {
            return;
        }

        if (Directory.Exists(local.Path))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(local.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failures.Add(
                $"LocalDisk.Path '{local.Path}' does not exist and cannot be created: {ex.Message}");
        }
    }

    private static void RequireNonEmpty(List<string> failures, string fieldName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{fieldName} is required when UseLitestream is true.");
        }
    }

    private static void ValidateRange(List<string> failures, string fieldName, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            failures.Add($"{fieldName} '{value}' must be in the range [{min}, {max}].");
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
                "Litestream credential environment variable '{EnvVarName}' is not set at " +
                "startup. The credential may be supplied later (systemd EnvironmentFile, " +
                "Docker secrets, AWS Secrets Manager). If it never arrives, Litestream " +
                "will fail at replication time.",
                envVarName);
        }
    }
}
