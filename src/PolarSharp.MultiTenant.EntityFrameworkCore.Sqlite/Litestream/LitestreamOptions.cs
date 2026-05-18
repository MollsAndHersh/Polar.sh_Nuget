namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

/// <summary>
/// Configuration options for the optional Litestream integration that ships with the
/// PolarSharp SQLite provider.
/// </summary>
/// <remarks>
/// <para>
/// Litestream (<see href="https://litestream.io"/>) is an external Go binary that performs
/// continuous streaming replication of SQLite database files to S3, Azure Blob Storage,
/// Google Cloud Storage, SFTP, or a local-disk target. PolarSharp does NOT bundle the
/// Litestream binary; the integration shipped here provides <em>integration affordances</em>
/// — startup config validation, a health check, and a litestream.yml template generator —
/// so the host can run Litestream as a sidecar / systemd unit / Windows service alongside
/// the application.
/// </para>
/// <para>
/// The entire integration is opt-in via <see cref="UseLitestream"/>. When the flag is
/// <see langword="false"/> (the default), the validator returns success without inspecting
/// any sub-fields, the health check reports <c>Healthy</c> with a "not enabled" message,
/// and the config-generator service is registered but inert. Hosts that never want
/// Litestream support pay zero runtime cost beyond a handful of unused DI registrations.
/// </para>
/// <para>
/// Bound from the <see cref="SectionName"/> configuration section
/// (<c>PolarSharp:MultiTenant:Sqlite:Litestream</c>) via
/// <c>services.AddPolarSqliteLitestream(configuration)</c>.
/// </para>
/// </remarks>
public sealed class LitestreamOptions
{
    /// <summary>The configuration section name bound to this options class.</summary>
    public const string SectionName = "PolarSharp:MultiTenant:Sqlite:Litestream";

    /// <summary>
    /// Master opt-in toggle. When <see langword="false"/> (default), no Litestream-related
    /// services run; the SQLite provider behaves as if Litestream support did not exist.
    /// When <see langword="true"/>, the configuration in this section is validated at
    /// startup and the integration affordances (health check, template generator, CLI
    /// helpers) become active.
    /// </summary>
    public bool UseLitestream { get; set; }

    /// <summary>
    /// The replication target type. Required when <see cref="UseLitestream"/> is
    /// <see langword="true"/>; ignored otherwise.
    /// </summary>
    public LitestreamReplicaTargetType ReplicaTargetType { get; set; } = LitestreamReplicaTargetType.S3;

    /// <summary>
    /// S3 (or S3-compatible such as Backblaze B2, Wasabi, or MinIO) replica config.
    /// Required when <see cref="ReplicaTargetType"/> is
    /// <see cref="LitestreamReplicaTargetType.S3"/>.
    /// </summary>
    public LitestreamS3Options S3 { get; set; } = new();

    /// <summary>
    /// Azure Blob Storage replica config. Required when <see cref="ReplicaTargetType"/> is
    /// <see cref="LitestreamReplicaTargetType.AzureBlob"/>.
    /// </summary>
    public LitestreamAzureBlobOptions AzureBlob { get; set; } = new();

    /// <summary>
    /// Google Cloud Storage replica config. Required when <see cref="ReplicaTargetType"/>
    /// is <see cref="LitestreamReplicaTargetType.GoogleCloudStorage"/>.
    /// </summary>
    public LitestreamGoogleCloudStorageOptions GoogleCloudStorage { get; set; } = new();

    /// <summary>
    /// SFTP replica config. Required when <see cref="ReplicaTargetType"/> is
    /// <see cref="LitestreamReplicaTargetType.Sftp"/>.
    /// </summary>
    public LitestreamSftpOptions Sftp { get; set; } = new();

    /// <summary>
    /// Local-disk replica config (primarily intended for tests and development).
    /// Required when <see cref="ReplicaTargetType"/> is
    /// <see cref="LitestreamReplicaTargetType.LocalDisk"/>.
    /// </summary>
    public LitestreamLocalDiskOptions LocalDisk { get; set; } = new();

    /// <summary>
    /// How often Litestream flushes WAL changes to the replica, in seconds. Default 1 second
    /// (matches Litestream's own default). Must be in the range <c>[1, 3600]</c>.
    /// </summary>
    public int SyncIntervalSeconds { get; set; } = 1;

    /// <summary>
    /// How often Litestream takes a full snapshot, in minutes. Default 60 minutes. Lower
    /// values increase replica object count but speed up point-in-time restores. Must be
    /// in the range <c>[1, 1440]</c>.
    /// </summary>
    public int SnapshotIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// How long Litestream retains snapshots before pruning, in days. Default 30 days.
    /// Affects both the point-in-time restore window and the storage cost on the replica
    /// target. Must be in the range <c>[1, 365]</c>.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Litestream's internal Prometheus metrics HTTP endpoint port. Default 9090.
    /// <see cref="LitestreamHealthCheck"/> pings this endpoint when
    /// <see cref="HealthCheckEnabled"/> is <see langword="true"/>.
    /// Must be in the range <c>[1024, 65535]</c>.
    /// </summary>
    public int MetricsPort { get; set; } = 9090;

    /// <summary>
    /// When <see langword="true"/> (default), the PolarSharp health check pings
    /// Litestream's <c>/metrics</c> endpoint at <see cref="MetricsPort"/> and surfaces
    /// replication lag in the host's <c>/health</c> endpoint.
    /// </summary>
    public bool HealthCheckEnabled { get; set; } = true;

    /// <summary>
    /// Maximum acceptable replication lag in seconds before the health check returns
    /// <c>Degraded</c>. Default 30 seconds. Must be in the range <c>[1, 3600]</c>.
    /// </summary>
    public int HealthCheckMaxLagSeconds { get; set; } = 30;

    /// <summary>
    /// When true, an IHostedService watches the SQLite database directory and automatically
    /// regenerates the litestream.yml + signals Litestream to reload its config whenever
    /// .db files are added or removed (e.g., new tenants onboarded, tenants removed).
    /// Default: false (simple manual-regeneration model is the default).
    /// </summary>
    /// <remarks>
    /// When enabled, the auto-regenerator service runs alongside the host application,
    /// using FileSystemWatcher to observe the database directory. Debounced regeneration
    /// avoids signal storms when multiple files change in quick succession (e.g., a bulk
    /// tenant onboarding script).
    /// </remarks>
    public bool AutoRegenerateOnTenantChange { get; set; }

    /// <summary>
    /// Filesystem path where the auto-regenerator writes the generated litestream.yml.
    /// Default: "/etc/litestream.yml" (matches Litestream's default config-file lookup).
    /// The directory must be writable by the host process.
    /// </summary>
    public string ConfigOutputPath { get; set; } = "/etc/litestream.yml";

    /// <summary>
    /// Filesystem path to Litestream's PID file, used by the auto-regenerator to signal
    /// the process to reload its configuration (SIGHUP on POSIX; not supported on Windows —
    /// see remarks). Default: "/var/run/litestream.pid".
    /// </summary>
    /// <remarks>
    /// Litestream supports SIGHUP for config reload on POSIX systems (Linux / macOS / BSD).
    /// On Windows, signal-based reload is not natively supported; the auto-regenerator
    /// logs a Warning on Windows and the operator must restart Litestream manually for
    /// config changes to take effect. Most production Litestream deployments run on Linux.
    /// </remarks>
    public string LitestreamPidFilePath { get; set; } = "/var/run/litestream.pid";

    /// <summary>
    /// Debounce window for the auto-regenerator. Multiple .db file changes within this
    /// window collapse into a single regeneration. Default: 2 seconds.
    /// </summary>
    public TimeSpan AutoRegenerateDebounceWindow { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>
/// The replication target type for the Litestream integration.
/// </summary>
/// <remarks>
/// Each value corresponds to a Litestream replica-type kind documented at
/// <see href="https://litestream.io/reference/config/"/>. The PolarSharp options validator
/// enforces that the sub-options object matching the chosen value has the required fields
/// populated.
/// </remarks>
public enum LitestreamReplicaTargetType
{
    /// <summary>Amazon S3 or any S3-compatible object store (MinIO, Backblaze B2, Wasabi).</summary>
    S3 = 0,

    /// <summary>Azure Blob Storage.</summary>
    AzureBlob = 1,

    /// <summary>Google Cloud Storage.</summary>
    GoogleCloudStorage = 2,

    /// <summary>SFTP (SSH File Transfer Protocol).</summary>
    Sftp = 3,

    /// <summary>A local-filesystem directory (primarily for tests and development).</summary>
    LocalDisk = 4,
}

/// <summary>
/// Configuration for an S3 or S3-compatible Litestream replica target.
/// </summary>
/// <remarks>
/// Credentials are NEVER stored in this options object directly — only the names of the
/// environment variables Litestream will read at runtime. This keeps the appsettings file
/// safe to commit.
/// </remarks>
public sealed class LitestreamS3Options
{
    /// <summary>The S3 bucket name. Required.</summary>
    public string Bucket { get; set; } = "";

    /// <summary>The S3 region. Default <c>us-east-1</c>. Required.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Path prefix prepended to every replicated database file in the bucket.
    /// Default <c>polarsharp/tenants/</c>.
    /// </summary>
    public string PathPrefix { get; set; } = "polarsharp/tenants/";

    /// <summary>
    /// Name of the environment variable holding the AWS access key ID Litestream uses.
    /// Default <c>AWS_ACCESS_KEY_ID</c>.
    /// </summary>
    public string AccessKeyIdEnvVar { get; set; } = "AWS_ACCESS_KEY_ID";

    /// <summary>
    /// Name of the environment variable holding the AWS secret access key Litestream uses.
    /// Default <c>AWS_SECRET_ACCESS_KEY</c>.
    /// </summary>
    public string SecretAccessKeyEnvVar { get; set; } = "AWS_SECRET_ACCESS_KEY";

    /// <summary>
    /// Endpoint URL for S3-compatible object stores (MinIO, Backblaze B2, Wasabi). Leave
    /// <see langword="null"/> to target AWS S3 with the default endpoint.
    /// </summary>
    public string? EndpointUrl { get; set; }

    /// <summary>
    /// When <see langword="true"/>, Litestream uses path-style URLs
    /// (<c>https://endpoint/bucket/key</c>) instead of virtual-hosted-style
    /// (<c>https://bucket.endpoint/key</c>). Required for MinIO and some other
    /// S3-compatible stores. Default <see langword="false"/>.
    /// </summary>
    public bool ForcePathStyle { get; set; }
}

/// <summary>
/// Configuration for an Azure Blob Storage Litestream replica target.
/// </summary>
public sealed class LitestreamAzureBlobOptions
{
    /// <summary>The Azure Storage account name. Required.</summary>
    public string AccountName { get; set; } = "";

    /// <summary>The blob container name. Default <c>polarsharp-tenants</c>. Required.</summary>
    public string Container { get; set; } = "polarsharp-tenants";

    /// <summary>Path prefix prepended to every replicated database file in the container.</summary>
    public string PathPrefix { get; set; } = "";

    /// <summary>
    /// Name of the environment variable holding the Azure Storage account key Litestream
    /// uses. Default <c>AZURE_STORAGE_KEY</c>.
    /// </summary>
    public string AccessKeyEnvVar { get; set; } = "AZURE_STORAGE_KEY";
}

/// <summary>
/// Configuration for a Google Cloud Storage Litestream replica target.
/// </summary>
public sealed class LitestreamGoogleCloudStorageOptions
{
    /// <summary>The GCS bucket name. Required.</summary>
    public string Bucket { get; set; } = "";

    /// <summary>
    /// Path prefix prepended to every replicated database file in the bucket.
    /// Default <c>polarsharp/tenants/</c>.
    /// </summary>
    public string PathPrefix { get; set; } = "polarsharp/tenants/";

    /// <summary>
    /// Absolute filesystem path to the Google service-account JSON credentials file
    /// Litestream will read. Required. Must point to an existing file.
    /// </summary>
    public string CredentialsJsonPath { get; set; } = "";
}

/// <summary>
/// Configuration for an SFTP Litestream replica target.
/// </summary>
public sealed class LitestreamSftpOptions
{
    /// <summary>The SFTP host. Required.</summary>
    public string Host { get; set; } = "";

    /// <summary>The SFTP port. Default 22.</summary>
    public int Port { get; set; } = 22;

    /// <summary>The SFTP login user. Required.</summary>
    public string User { get; set; } = "";

    /// <summary>
    /// Remote path under which replicas are stored on the SFTP host.
    /// Default <c>/srv/polarsharp/tenants/</c>. Required.
    /// </summary>
    public string Path { get; set; } = "/srv/polarsharp/tenants/";

    /// <summary>
    /// Absolute filesystem path to the SSH private key Litestream uses to authenticate.
    /// Required. Must point to an existing file.
    /// </summary>
    public string PrivateKeyPath { get; set; } = "";
}

/// <summary>
/// Configuration for a local-disk Litestream replica target. Intended primarily for
/// tests, development, and as a fail-safe second replica.
/// </summary>
public sealed class LitestreamLocalDiskOptions
{
    /// <summary>
    /// Absolute filesystem path to the local-disk replica directory.
    /// Default <c>/var/lib/polarsharp/litestream-replica/</c>. Required.
    /// The validator creates the directory if absent.
    /// </summary>
    public string Path { get; set; } = "/var/lib/polarsharp/litestream-replica/";
}
