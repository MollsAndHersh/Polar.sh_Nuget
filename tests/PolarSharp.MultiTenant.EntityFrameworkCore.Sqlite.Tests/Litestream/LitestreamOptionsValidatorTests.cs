using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Tests.Litestream;

/// <summary>
/// Tests for <see cref="LitestreamOptionsValidator"/> — the <see cref="IValidateOptions{TOptions}"/>
/// implementation that gates host startup when the Litestream config is malformed.
/// </summary>
/// <remarks>
/// <para>
/// The validator is a strict no-op when <see cref="LitestreamOptions.UseLitestream"/> is
/// <c>false</c>. Per-target validation only fires when the master toggle is on; tests cover
/// each replica-target shape (S3, AzureBlob, GoogleCloudStorage, Sftp, LocalDisk) plus the
/// range checks on the numeric tuning parameters.
/// </para>
/// </remarks>
public sealed class LitestreamOptionsValidatorTests
{
    // --- Master toggle off short-circuits ---------------------------------------------

    [Fact]
    public void Validate_returns_Success_when_UseLitestream_is_false()
    {
        // Every sub-field is at its default (mostly empty strings) — what would otherwise be
        // a wall of failures becomes a no-op because the master toggle is off.
        var options = new LitestreamOptions { UseLitestream = false };
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    // --- S3 target --------------------------------------------------------------------

    [Fact]
    public void Validate_S3_succeeds_when_required_fields_populated()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData("Bucket")]
    [InlineData("Region")]
    [InlineData("AccessKeyIdEnvVar")]
    [InlineData("SecretAccessKeyEnvVar")]
    public void Validate_S3_fails_when_required_field_missing(string fieldName)
    {
        var opts = TestHelpers.FullyEnabledOptions();
        switch (fieldName)
        {
            case "Bucket": opts.S3.Bucket = ""; break;
            case "Region": opts.S3.Region = ""; break;
            case "AccessKeyIdEnvVar": opts.S3.AccessKeyIdEnvVar = ""; break;
            case "SecretAccessKeyEnvVar": opts.S3.SecretAccessKeyEnvVar = ""; break;
        }
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains(fieldName, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_S3_EndpointUrl_optional_when_unset()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.S3.EndpointUrl = null;
        var sut = NewValidator(out _);

        Assert.True(sut.Validate(name: null, opts).Succeeded);
    }

    [Fact]
    public void Validate_S3_EndpointUrl_must_be_valid_absolute_URI_when_set()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.S3.EndpointUrl = "not a valid uri";
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("EndpointUrl", StringComparison.Ordinal));
    }

    // --- AzureBlob target -------------------------------------------------------------

    [Fact]
    public void Validate_AzureBlob_succeeds_when_required_fields_populated()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.AzureBlob;
        opts.AzureBlob = new LitestreamAzureBlobOptions
        {
            AccountName = "mystorage",
            Container = "polarsharp-tenants",
            AccessKeyEnvVar = "AZURE_STORAGE_KEY",
        };
        var sut = NewValidator(out _);

        Assert.True(sut.Validate(name: null, opts).Succeeded);
    }

    [Theory]
    [InlineData("AccountName")]
    [InlineData("Container")]
    [InlineData("AccessKeyEnvVar")]
    public void Validate_AzureBlob_fails_when_required_field_missing(string fieldName)
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.AzureBlob;
        opts.AzureBlob = new LitestreamAzureBlobOptions
        {
            AccountName = "mystorage",
            Container = "polarsharp-tenants",
            AccessKeyEnvVar = "AZURE_STORAGE_KEY",
        };
        switch (fieldName)
        {
            case "AccountName": opts.AzureBlob.AccountName = ""; break;
            case "Container": opts.AzureBlob.Container = ""; break;
            case "AccessKeyEnvVar": opts.AzureBlob.AccessKeyEnvVar = ""; break;
        }
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains(fieldName, StringComparison.Ordinal));
    }

    // --- GoogleCloudStorage target ----------------------------------------------------

    [Fact]
    public void Validate_GoogleCloudStorage_succeeds_when_credentials_file_exists()
    {
        using var temp = new TempDirectoryFixture();
        var credsPath = temp.TouchFile("gcs-creds.json", "{\"type\":\"service_account\"}");

        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.GoogleCloudStorage;
        opts.GoogleCloudStorage = new LitestreamGoogleCloudStorageOptions
        {
            Bucket = "gcs-bucket",
            CredentialsJsonPath = credsPath,
        };
        var sut = NewValidator(out _);

        Assert.True(sut.Validate(name: null, opts).Succeeded);
    }

    [Fact]
    public void Validate_GoogleCloudStorage_fails_when_credentials_file_missing()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.GoogleCloudStorage;
        opts.GoogleCloudStorage = new LitestreamGoogleCloudStorageOptions
        {
            Bucket = "gcs-bucket",
            CredentialsJsonPath = "/nonexistent/path/to/creds.json",
        };
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("CredentialsJsonPath", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_GoogleCloudStorage_fails_when_Bucket_missing()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.GoogleCloudStorage;
        opts.GoogleCloudStorage = new LitestreamGoogleCloudStorageOptions
        {
            Bucket = "",
            CredentialsJsonPath = "irrelevant",
        };
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("Bucket", StringComparison.Ordinal));
    }

    // --- Sftp target ------------------------------------------------------------------

    [Fact]
    public void Validate_Sftp_succeeds_when_required_fields_and_key_file_exist()
    {
        using var temp = new TempDirectoryFixture();
        var keyPath = temp.TouchFile("id_rsa", "-----BEGIN OPENSSH PRIVATE KEY-----\n");

        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.Sftp;
        opts.Sftp = new LitestreamSftpOptions
        {
            Host = "backup.example.com",
            User = "polar",
            Path = "/srv/polarsharp/tenants/",
            PrivateKeyPath = keyPath,
        };
        var sut = NewValidator(out _);

        Assert.True(sut.Validate(name: null, opts).Succeeded);
    }

    [Theory]
    [InlineData("Host")]
    [InlineData("User")]
    [InlineData("Path")]
    [InlineData("PrivateKeyPath")]
    public void Validate_Sftp_fails_when_required_field_missing(string fieldName)
    {
        using var temp = new TempDirectoryFixture();
        var keyPath = temp.TouchFile("id_rsa", "key");

        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.Sftp;
        opts.Sftp = new LitestreamSftpOptions
        {
            Host = "host.example",
            User = "user",
            Path = "/path/",
            PrivateKeyPath = keyPath,
        };
        switch (fieldName)
        {
            case "Host": opts.Sftp.Host = ""; break;
            case "User": opts.Sftp.User = ""; break;
            case "Path": opts.Sftp.Path = ""; break;
            case "PrivateKeyPath": opts.Sftp.PrivateKeyPath = ""; break;
        }
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains(fieldName, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Sftp_fails_when_PrivateKeyPath_points_to_missing_file()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.Sftp;
        opts.Sftp = new LitestreamSftpOptions
        {
            Host = "host.example",
            User = "user",
            Path = "/path/",
            PrivateKeyPath = "/nonexistent/key/path",
        };
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f =>
            f.Contains("PrivateKeyPath", StringComparison.Ordinal) &&
            f.Contains("existing file", StringComparison.OrdinalIgnoreCase));
    }

    // --- LocalDisk target -------------------------------------------------------------

    [Fact]
    public void Validate_LocalDisk_succeeds_when_directory_exists()
    {
        using var temp = new TempDirectoryFixture();
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.LocalDisk;
        opts.LocalDisk = new LitestreamLocalDiskOptions { Path = temp.Path };
        var sut = NewValidator(out _);

        Assert.True(sut.Validate(name: null, opts).Succeeded);
    }

    [Fact]
    public void Validate_LocalDisk_creates_directory_when_absent_and_creatable()
    {
        using var temp = new TempDirectoryFixture();
        var creatable = Path.Combine(temp.Path, "to-be-created");
        Assert.False(Directory.Exists(creatable));

        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.LocalDisk;
        opts.LocalDisk = new LitestreamLocalDiskOptions { Path = creatable };
        var sut = NewValidator(out _);

        Assert.True(sut.Validate(name: null, opts).Succeeded);
        Assert.True(Directory.Exists(creatable), "Validator should have created the directory.");
    }

    [Fact]
    public void Validate_LocalDisk_fails_when_Path_missing()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.LocalDisk;
        opts.LocalDisk = new LitestreamLocalDiskOptions { Path = "" };
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("LocalDisk.Path", StringComparison.Ordinal));
    }

    // --- Numeric range checks ---------------------------------------------------------

    [Theory]
    [InlineData(nameof(LitestreamOptions.SyncIntervalSeconds), 1, true)]
    [InlineData(nameof(LitestreamOptions.SyncIntervalSeconds), 0, false)]
    [InlineData(nameof(LitestreamOptions.SyncIntervalSeconds), 3601, false)]
    [InlineData(nameof(LitestreamOptions.SnapshotIntervalMinutes), 1, true)]
    [InlineData(nameof(LitestreamOptions.SnapshotIntervalMinutes), 0, false)]
    [InlineData(nameof(LitestreamOptions.SnapshotIntervalMinutes), 1441, false)]
    [InlineData(nameof(LitestreamOptions.RetentionDays), 1, true)]
    [InlineData(nameof(LitestreamOptions.RetentionDays), 0, false)]
    [InlineData(nameof(LitestreamOptions.RetentionDays), 366, false)]
    [InlineData(nameof(LitestreamOptions.MetricsPort), 1024, true)]
    [InlineData(nameof(LitestreamOptions.MetricsPort), 1023, false)]
    [InlineData(nameof(LitestreamOptions.MetricsPort), 65535, true)]
    [InlineData(nameof(LitestreamOptions.MetricsPort), 65536, false)]
    [InlineData(nameof(LitestreamOptions.HealthCheckMaxLagSeconds), 1, true)]
    [InlineData(nameof(LitestreamOptions.HealthCheckMaxLagSeconds), 0, false)]
    [InlineData(nameof(LitestreamOptions.HealthCheckMaxLagSeconds), 3601, false)]
    public void Validate_numeric_range_boundaries(string fieldName, int value, bool expectSuccess)
    {
        var opts = TestHelpers.FullyEnabledOptions();
        switch (fieldName)
        {
            case nameof(LitestreamOptions.SyncIntervalSeconds): opts.SyncIntervalSeconds = value; break;
            case nameof(LitestreamOptions.SnapshotIntervalMinutes): opts.SnapshotIntervalMinutes = value; break;
            case nameof(LitestreamOptions.RetentionDays): opts.RetentionDays = value; break;
            case nameof(LitestreamOptions.MetricsPort): opts.MetricsPort = value; break;
            case nameof(LitestreamOptions.HealthCheckMaxLagSeconds): opts.HealthCheckMaxLagSeconds = value; break;
        }
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);
        Assert.Equal(expectSuccess, result.Succeeded);
        if (!expectSuccess)
        {
            Assert.Contains(result.Failures!, f => f.Contains(fieldName, StringComparison.Ordinal));
        }
    }

    // --- AutoRegenerateOnTenantChange branch ------------------------------------------

    [Fact]
    public void Validate_AutoRegenerate_true_requires_ConfigOutputPath_and_PidFilePath_and_positive_debounce()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.AutoRegenerateOnTenantChange = true;
        opts.ConfigOutputPath = "";
        opts.LitestreamPidFilePath = "";
        opts.AutoRegenerateDebounceWindow = TimeSpan.Zero;
        var sut = NewValidator(out _);

        var result = sut.Validate(name: null, opts);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("ConfigOutputPath", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, f => f.Contains("LitestreamPidFilePath", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, f => f.Contains("AutoRegenerateDebounceWindow", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AutoRegenerate_false_does_not_check_paths_or_debounce()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.AutoRegenerateOnTenantChange = false;
        opts.ConfigOutputPath = "";
        opts.LitestreamPidFilePath = "";
        opts.AutoRegenerateDebounceWindow = TimeSpan.Zero;
        var sut = NewValidator(out _);

        Assert.True(sut.Validate(name: null, opts).Succeeded);
    }

    // --- Env-var existence warnings ---------------------------------------------------

    [Fact]
    public void Validate_logs_Warning_for_missing_S3_env_vars_but_does_not_fail()
    {
        const string FakeAccessKeyVar = "POLAR_TEST_FAKE_AWS_ACCESS_KEY_ID_UNSET";
        const string FakeSecretVar = "POLAR_TEST_FAKE_AWS_SECRET_ACCESS_KEY_UNSET";
        // Clear any pre-existing values so the warning branch fires deterministically.
        using var clearAccess = new EnvVarScope(FakeAccessKeyVar, value: null);
        using var clearSecret = new EnvVarScope(FakeSecretVar, value: null);

        var opts = TestHelpers.FullyEnabledOptions();
        opts.S3.AccessKeyIdEnvVar = FakeAccessKeyVar;
        opts.S3.SecretAccessKeyEnvVar = FakeSecretVar;
        var sut = NewValidator(out var log);

        var result = sut.Validate(name: null, opts);
        Assert.True(result.Succeeded, result.FailureMessage);
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains(FakeAccessKeyVar, StringComparison.Ordinal));
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains(FakeSecretVar, StringComparison.Ordinal));
    }

    // --- Helpers ----------------------------------------------------------------------

    private static LitestreamOptionsValidator NewValidator(out RecordingLogger<LitestreamOptionsValidator> log)
    {
        log = new RecordingLogger<LitestreamOptionsValidator>();
        return new LitestreamOptionsValidator(log);
    }
}
