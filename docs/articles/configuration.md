# Configuration Reference

All PolarSharp settings live under the `PolarSharp` key in `appsettings.json`. Every setting shown here is optional except `AccessToken`.

## Full `appsettings.json` example

```json
{
  "PolarSharp": {
    "Mode":        "Test",
    "AccessToken": "tok_sandbox_xxx",
    "CustomBaseUrl": null,
    "BasePath":    "/v1",
    "ApiVersion":  null,
    "ApiVersionStrictness": "Warn",
    "TimeoutMs":   30000,
    "MaxRetries":  3,
    "Resilience": {
      "CircuitBreakerFailureThreshold": 5,
      "CircuitBreakerSamplingSeconds":  30,
      "CircuitBreakerBreakSeconds":     15,
      "HedgeAfterMs": null
    },
    "Connection": {
      "MaxConnectionsPerServer":            100,
      "PooledConnectionLifetimeMinutes":    15,
      "PooledConnectionIdleTimeoutMinutes": 2,
      "EnableHttp2": true,
      "EnableHttp3": false,
      "EnableMultipleHttp2Connections": true
    },
    "Webhooks": {
      "Secret":           "whsec_xxx",
      "Path":             "/hooks/polar",
      "RequireHttps":     true,
      "ToleranceSeconds": 300
    }
  }
}
```

## Core settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Mode` | `"Test"` \| `"Live"` \| `"Custom"` | `"Test"` | Target environment. `"Test"` uses Polar's sandbox — no real charges. `"Live"` uses production — real transactions. |
| `AccessToken` | string | *(required)* | Polar Organization Access Token. For sandbox: `tok_sandbox_…`. For production: `tok_live_…`. Keep in user-secrets or a secret manager. |
| `CustomBaseUrl` | string? | `null` | Required when `Mode = "Custom"`. Must be an absolute HTTPS URI. |
| `BasePath` | string | `"/v1"` | URL path prefix for all requests. Changes rarely; only update when Polar releases `/v2/`. |
| `ApiVersion` | string? | SDK default | ISO date pinning the Polar API schema version sent as `Polar-Version` header. `null` = SDK's bundled version. See [API Versioning](api-versioning.md). |
| `ApiVersionStrictness` | `"Warn"` \| `"Strict"` \| `"Off"` | `"Warn"` | What to do when `ApiVersion` differs from the SDK's bundled version. `"Strict"` fails startup. |
| `TimeoutMs` | int | `30000` | Per-attempt HTTP timeout in milliseconds. Range: 1000–300000. |
| `MaxRetries` | int | `3` | Maximum retry attempts. Range: 0–10. |

## Resilience sub-options (`PolarSharp:Resilience`)

| Key | Default | Description |
|-----|---------|-------------|
| `CircuitBreakerFailureThreshold` | `5` | Number of consecutive failures before the circuit opens. |
| `CircuitBreakerSamplingSeconds` | `30` | Window in which failures are counted. |
| `CircuitBreakerBreakSeconds` | `15` | How long the circuit stays open before allowing probe requests. |
| `HedgeAfterMs` | `null` (disabled) | If set, sends a duplicate `GET`/`HEAD` after this many ms to reduce tail latency. Never applied to mutating requests. |

## Connection sub-options (`PolarSharp:Connection`)

| Key | Default | Description |
|-----|---------|-------------|
| `MaxConnectionsPerServer` | `100` | Caps simultaneous TCP connections to Polar per host. |
| `PooledConnectionLifetimeMinutes` | `15` | Forces TCP reconnection at this interval — refreshes DNS for cloud load balancer rotation. |
| `PooledConnectionIdleTimeoutMinutes` | `2` | Closes idle connections after this period to reclaim memory. |
| `EnableHttp2` | `true` | Use HTTP/2 multiplexing (one TCP connection for many concurrent requests). |
| `EnableHttp3` | `false` | Experimental QUIC/HTTP3 (opt-in; server support varies). |
| `EnableMultipleHttp2Connections` | `true` | Allow multiple simultaneous HTTP/2 connections (increases parallelism under burst load). |

## Webhook sub-options (`PolarSharp:Webhooks`)

Only used when `AddPolarWebhooks()` is called. See [Webhooks](webhooks.md).

| Key | Default | Description |
|-----|---------|-------------|
| `Secret` | `null` | Shorthand for a single webhook secret (`whsec_…`). Treated as a one-element `Secrets` list. |
| `Secrets` | `[]` | List of active webhook secrets. Supports zero-downtime rotation — verification passes if any secret matches. |
| `Path` | `"/hooks/polar"` | Route that receives Polar webhook deliveries. |
| `RequireHttps` | `true` | Reject non-HTTPS requests with 400 (not a redirect). |
| `ToleranceSeconds` | `300` | Maximum allowed age of a webhook timestamp before replay-protection rejects it. |

## Startup validation

Missing `AccessToken` or invalid settings fail startup immediately with a clear error message, before any HTTP traffic is served. Set `ASPNETCORE_ENVIRONMENT=Development` to get the full exception detail.

## Multi-tenant: single-tenant -> MT upgrade (`PolarSharp:MultiTenant:SingleTenantUpgrade`)

Bound by `services.AddPolarSingleTenantUpgrade(configuration)` from `PolarSharp.MultiTenant.EntityFrameworkCore`. Drives the one-time backfill that runs the first time a host that was previously running in single-tenant mode boots up with multi-tenant mode enabled.

| Key | Type | Default | Required | Validation / Notes |
|-----|------|---------|----------|--------------------|
| `EnableAutomaticUpgrade` | `bool` | `true` | optional | When `true`, the hosted service runs the upgrade on first MT-mode boot. Set `false` to invoke the upgrade explicitly via `dotnet polar-mt upgrade` during a chosen maintenance window. |
| `DefaultTenantStrategy` | enum | `LiteralDefault` | optional | One of `LiteralDefault` (auto-create a single named tenant), `FirstUserOrganization` (resolve from the Identity package's user-to-org mapping), `HostSupplied` (delegate to an `IDefaultTenantResolver` the host registers). `FirstUserOrganization` throws `NotSupportedException` until the Identity package's MT integration ships. |
| `LiteralDefaultTenantSlug` | `string` | `"default"` | required when strategy is `LiteralDefault` | Slug pattern: lowercase alphanumeric and hyphens, no leading or trailing hyphens, length 1–64. |
| `LiteralDefaultTenantName` | `string` | `"Default Tenant"` | required when strategy is `LiteralDefault` | Display name for the auto-created tenant. |
| `RequireGracefulQuiescence` | `bool` | `true` | optional | Refuses to run unless the host has signalled quiescence. Set `false` only on low-traffic systems where briefly serving partially-stamped reads is acceptable. |
| `MaxRunDuration` | `TimeSpan` | `00:30:00` | optional | Hard wall-clock cap. Hosts with very large single-tenant datasets (millions of rows across many tables) may need to increase. |

Consumed by `PolarSharp.MultiTenant.EntityFrameworkCore` (the hosted-service orchestration) and each provider-specific migrator package (`PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite`, `.SqlServer`, `.Postgres`, `.MariaDb`, `.CosmosDb`).

## Multi-tenant: SQLite Litestream replication (`PolarSharp:MultiTenant:Sqlite:Litestream`)

Bound by `services.AddPolarSqliteLitestream(configuration)` (called transitively by `UseSqlite(...)`). The entire section is gated by `UseLitestream`; with the toggle off, the validator is a no-op and every sub-field is ignored.

| Key | Type | Default | Required when `UseLitestream=true` | Validation / Notes |
|-----|------|---------|------------------------------------|--------------------|
| `UseLitestream` | `bool` | `false` | n/a (the master toggle) | When `false`, no Litestream-related services run. |
| `ReplicaTargetType` | enum | `S3` | required | One of `S3`, `AzureBlob`, `GoogleCloudStorage`, `Sftp`, `LocalDisk`. The matching sub-options object must be populated. |
| `SyncIntervalSeconds` | `int` | `1` | optional | Range `[1, 3600]`. How often Litestream flushes WAL changes to the replica. |
| `SnapshotIntervalMinutes` | `int` | `60` | optional | Range `[1, 1440]`. Lower values speed point-in-time restores at the cost of more replica objects. |
| `RetentionDays` | `int` | `30` | optional | Range `[1, 365]`. Affects the point-in-time restore window and replica storage cost. |
| `MetricsPort` | `int` | `9090` | optional | Range `[1024, 65535]`. Litestream's Prometheus metrics endpoint port. |
| `HealthCheckEnabled` | `bool` | `true` | optional | When `true`, the PolarSharp health check pings `/metrics` and surfaces lag. |
| `HealthCheckMaxLagSeconds` | `int` | `30` | optional | Range `[1, 3600]`. Threshold for `Degraded` vs. `Healthy`. |
| `AutoRegenerateOnTenantChange` | `bool` | `false` | optional | When `true`, the auto-regenerator `IHostedService` watches `.db` files + lifecycle notifications and signals Litestream via SIGHUP on POSIX. Windows hosts log a Warning instead. |
| `ConfigOutputPath` | `string` | `"/etc/litestream.yml"` | required when auto-regen enabled | Where the generator writes the YAML. Directory must be writable by the host process. |
| `LitestreamPidFilePath` | `string` | `"/var/run/litestream.pid"` | required when auto-regen enabled | Path to Litestream's PID file (Litestream must be started with `--pidfile`). |
| `AutoRegenerateDebounceWindow` | `TimeSpan` | `00:00:02` | optional | Collapses bursts of file/lifecycle events into one regeneration. |

### `S3` sub-options (`PolarSharp:MultiTenant:Sqlite:Litestream:S3`)

Required when `ReplicaTargetType = S3`. Also covers MinIO / Backblaze B2 / Wasabi via `EndpointUrl` + `ForcePathStyle`.

| Key | Type | Default | Required | Notes |
|-----|------|---------|----------|-------|
| `Bucket` | `string` | `""` | required | S3 bucket name. |
| `Region` | `string` | `"us-east-1"` | required | AWS region (or compatible store's region). |
| `PathPrefix` | `string` | `"polarsharp/tenants/"` | optional | Prefix prepended to every replicated `.db` file. |
| `AccessKeyIdEnvVar` | `string` | `"AWS_ACCESS_KEY_ID"` | required | **Name** of the env var. Never the value. |
| `SecretAccessKeyEnvVar` | `string` | `"AWS_SECRET_ACCESS_KEY"` | required | **Name** of the env var. Never the value. |
| `EndpointUrl` | `string?` | `null` | optional | Non-null for S3-compatible stores. |
| `ForcePathStyle` | `bool` | `false` | optional | Required for MinIO and some other S3-compatible stores. |

### `AzureBlob` sub-options (`PolarSharp:MultiTenant:Sqlite:Litestream:AzureBlob`)

Required when `ReplicaTargetType = AzureBlob`.

| Key | Type | Default | Required | Notes |
|-----|------|---------|----------|-------|
| `AccountName` | `string` | `""` | required | Azure Storage account name. |
| `Container` | `string` | `"polarsharp-tenants"` | required | Blob container name. |
| `PathPrefix` | `string` | `""` | optional | Prefix prepended to every replicated `.db` file. |
| `AccessKeyEnvVar` | `string` | `"AZURE_STORAGE_KEY"` | required | **Name** of the env var holding the Storage account key. |

### `GoogleCloudStorage` sub-options (`PolarSharp:MultiTenant:Sqlite:Litestream:GoogleCloudStorage`)

Required when `ReplicaTargetType = GoogleCloudStorage`.

| Key | Type | Default | Required | Notes |
|-----|------|---------|----------|-------|
| `Bucket` | `string` | `""` | required | GCS bucket name. |
| `PathPrefix` | `string` | `"polarsharp/tenants/"` | optional | Prefix prepended to every replicated `.db` file. |
| `CredentialsJsonPath` | `string` | `""` | required | Absolute filesystem path to a service-account JSON file. Validator confirms the file exists. |

### `Sftp` sub-options (`PolarSharp:MultiTenant:Sqlite:Litestream:Sftp`)

Required when `ReplicaTargetType = Sftp`.

| Key | Type | Default | Required | Notes |
|-----|------|---------|----------|-------|
| `Host` | `string` | `""` | required | SFTP host. |
| `Port` | `int` | `22` | optional | SFTP port. |
| `User` | `string` | `""` | required | SFTP login user. |
| `Path` | `string` | `"/srv/polarsharp/tenants/"` | required | Remote path on the SFTP host. |
| `PrivateKeyPath` | `string` | `""` | required | Absolute filesystem path to the SSH private key. Validator confirms the file exists. |

### `LocalDisk` sub-options (`PolarSharp:MultiTenant:Sqlite:Litestream:LocalDisk`)

Required when `ReplicaTargetType = LocalDisk`. Primarily for tests and development.

| Key | Type | Default | Required | Notes |
|-----|------|---------|----------|-------|
| `Path` | `string` | `"/var/lib/polarsharp/litestream-replica/"` | required | Local replica directory. Validator creates if absent. |

### Credentials policy

All credentials are referenced by **environment variable name**, never embedded in appsettings. Supply the actual values via systemd `EnvironmentFile=`, Docker secrets, AWS Secrets Manager, or whatever the platform uses. The validator logs a Warning at startup if a referenced env var is unset but does NOT fail — secrets often arrive after process start through indirection.

Consumed by `PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite`.

## Multi-tenant: tenant lifecycle (`PolarSharp:MultiTenant:TenantStatus`)

Bound by `services.AddPolarTenantLifecycle(configuration)` from `PolarSharp.MultiTenant`. Configures the `ITenantStatusService` policy knobs that gate suspension and govern the soft-delete retention window.

| Key | Type | Default | Required | Validation / Notes |
|-----|------|---------|----------|--------------------|
| `RequireVerifiedEmailForSuspension` | `bool` | `true` | optional | When `true`, `SuspendAsync` refuses to suspend a tenant whose `SiteManagerEmailVerified=false` — the suspension notification would be unverifiable. Set `false` to globally relax. |
| `SuspendUnverifiedTenantsAnyway` | `bool` | `false` | optional | Per-call override that preserves the global `RequireVerifiedEmailForSuspension` requirement but bypasses on this specific call. Provided as a separate flag so the audit trail captures **which** form of relaxation was applied. |
| `DeletedTenantRetentionDays` | `int` | `90` | optional | Retention period for soft-deleted tenants before a separate cleanup process (out of scope for `ITenantStatusService`) permanently removes the data. |

Consumed by `PolarSharp.MultiTenant`.

## Multi-tenant: site-manager notifications (`PolarSharp:MultiTenant:Notifications`)

Bound by `services.AddPolarMultiTenantNotifications(configuration)` from `PolarSharp.MultiTenant.Notifications`. **Opt-in at two levels**: the host has to install the NuGet AND set `Enabled = true`.

| Key | Type | Default | Required | Validation / Notes |
|-----|------|---------|----------|--------------------|
| `Enabled` | `bool` | `false` | n/a (the master toggle) | When `false`, the registered MediatR handler runs but immediately returns — no validation cost, no outbound HTTP. |
| `EnabledChannels.Email` | `bool` | `true` | optional | Whether the email channel dispatches. |
| `EnabledChannels.Sms` | `bool` | `false` | optional | Whether the SMS channel dispatches. |
| `EnabledChannels.Webhook` | `bool` | `false` | optional | Whether the webhook channel dispatches. |
| `SendToUnverifiedEmail` | `bool` | `false` | optional | When `false`, the email channel skips recipients with `SiteManagerEmailVerified=false`. SMS + webhook are unaffected. |

### `Email` sub-options (`PolarSharp:MultiTenant:Notifications:Email`)

| Key | Type | Default | Required when email channel enabled | Notes |
|-----|------|---------|--------------------------------------|-------|
| `Provider` | enum | `SendGrid` | required | Only `SendGrid` is supported in v1.0. |
| `FromAddress` | `string` | `""` | required | The `From:` header on outgoing messages. |
| `FromDisplayName` | `string` | `"PolarSharp Platform"` | optional | Display name on the `From:` header. |
| `SendGrid.ApiKeyEnvVar` | `string` | `"SENDGRID_API_KEY"` | required | **Name** of the env var holding the SendGrid API key. |

### `Sms` sub-options (`PolarSharp:MultiTenant:Notifications:Sms`)

| Key | Type | Default | Required when SMS channel enabled | Notes |
|-----|------|---------|------------------------------------|-------|
| `Provider` | enum | `Twilio` | required | Only `Twilio` is supported in v1.0. |
| `Twilio.AccountSidEnvVar` | `string` | `"TWILIO_ACCOUNT_SID"` | required | **Name** of the env var. |
| `Twilio.AuthTokenEnvVar` | `string` | `"TWILIO_AUTH_TOKEN"` | required | **Name** of the env var. |
| `Twilio.FromNumber` | `string` | `""` | required | Twilio-provisioned `From:` number in E.164 format (e.g., `+15558675309`). |

### `Webhook` sub-options (`PolarSharp:MultiTenant:Notifications:Webhook`)

| Key | Type | Default | Required when webhook channel enabled | Notes |
|-----|------|---------|----------------------------------------|-------|
| `Url` | `string` | `""` | required | HTTPS URL the webhook POSTs the JSON payload to. |
| `SigningSecretEnvVar` | `string` | `"POLARSHARP_WEBHOOK_SECRET"` | required | **Name** of the env var holding the HMAC signing secret. Receiver verifies the `X-PolarSharp-Signature: sha256={hex}` header. |
| `TimeoutSeconds` | `int` | `10` | optional | Range `[1, 300]`. |

### `Templates` sub-options (`PolarSharp:MultiTenant:Notifications:Templates`)

One template object per status transition: `Suspended`, `Reactivated`, `Deactivated`, `Deleted`. Each carries `EmailSubject`, `EmailBody`, `SmsBody` (all support placeholder substitution: `{TenantName}`, `{TenantIdentifier}`, `{NewStatus}`, `{PreviousStatus}`, `{Reason}`, `{OccurredAt}`). Sensible defaults ship per transition; hosts can override any individual field.

### Credentials policy

All channel credentials are referenced by **environment variable name**, never embedded in appsettings. The validator logs a Warning at startup if a referenced env var is unset but does NOT fail — secrets often arrive after process start through indirection.

Consumed by `PolarSharp.MultiTenant.Notifications`.
