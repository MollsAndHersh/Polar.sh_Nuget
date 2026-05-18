# PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite

SQLite provider for PolarSharp's EF Core-backed tenant store. Uses **physical file isolation** — each tenant's catalog/identity DB lives in its own `{tenantId}.db` file. Platform-level data (the tenant registry and the upgrade-history table) lives in a shared `master_SaaS.db`.

## Install

```bash
dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseSqlite("/var/lib/polarsharp/tenants/");
```

Directory contents after onboarding two tenants:

```
/var/lib/polarsharp/tenants/
├── master_SaaS.db                                         ← platform data (registry + upgrade history)
├── 3f7c2a4e-9d8b-4e15-a7c1-1b2c3d4e5f6a.db              ← Tenant A's catalog/identity
└── b81c5d2f-0e2a-4b6c-9d8e-5f6a7b8c9d0e.db              ← Tenant B's catalog/identity
```

The shared `master_SaaS.db` opens in WAL journal mode by default — required by Litestream replication (Stage C) and the right default for any non-trivial workload. Per-tenant DBs use the default journal mode.

## Platform data: `master_SaaS.db`

The master file holds everything that describes the deployment rather than any one tenant's data:

- The tenant registry (`polar_tenants`)
- The one-time upgrade-history table (`polar_upgrade_history`)
- Future cross-tenant platform tables as they ship

Nothing tenant-owned ever touches `master_SaaS.db`. The naming makes the platform / tenant boundary obvious to operators inspecting the filesystem.

### Migrating from `__tenants.db`

The pre-v1.2 SQLite provider used `__tenants.db` for the registry. When the provider starts up and finds a legacy `__tenants.db` (and no `master_SaaS.db`), it falls back to the legacy filename for the current run and logs a warning. Rename the file during a maintenance window — either manually, or by running the single-tenant upgrade migrator (see below), which renames as part of its work.

## Single-tenant -> multi-tenant upgrade

Hosts that started life as single-tenant deployments can opt into the automated upgrade migrator:

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseSqlite("/var/lib/polarsharp/tenants/");

builder.Services.AddPolarSingleTenantUpgrade(builder.Configuration);
```

On the next boot, the migrator:

1. Confirms `master_SaaS.db` is in place (using the legacy fallback when needed).
2. Inserts the default tenant into the registry — or recognises an existing single-tenant entry as the default.
3. Renames any legacy `data.db` or `app.db` in the database directory to `{defaultTenantId}.db`.
4. Records a row in `polar_upgrade_history` so subsequent boots short-circuit.

The migrator never deletes anything. A surviving `__tenants.db` alongside `master_SaaS.db` triggers a warning recommending manual review.

See `docs/articles/tenant-isolation.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/) for the full file-isolation rationale and SQLite-specific tuning.

## Litestream integration (opt-in)

[Litestream](https://litestream.io) is an external Go binary that continuously streams the SQLite WAL to S3, Azure Blob Storage, Google Cloud Storage, SFTP, or a local directory. It watches the `.db` files at the filesystem level — the application never talks to Litestream directly. PolarSharp ships **integration affordances**: startup config validation, a health check, a `litestream.yml` template generator, and CLI helper scaffolding. The Litestream binary itself is *not* bundled — install it separately on each host that needs replication.

### Opting in

Two layers gate activation:

1. The `UseSqlite(...)` call registers the Litestream services by default (pass `enableLitestream: false` to opt out entirely).
2. The host sets `PolarSharp:MultiTenant:Sqlite:Litestream:UseLitestream = true` in `appsettings.json` to actually activate the integration. With the toggle off, the validator is a no-op, the health check reports "not enabled", and the config generator is idle.

### `appsettings.json` example (S3)

```json
{
  "PolarSharp": {
    "MultiTenant": {
      "Sqlite": {
        "Litestream": {
          "UseLitestream": true,
          "ReplicaTargetType": "S3",
          "S3": {
            "Bucket": "acme-polarsharp-replicas",
            "Region": "us-east-1",
            "PathPrefix": "prod/tenants/",
            "AccessKeyIdEnvVar": "AWS_ACCESS_KEY_ID",
            "SecretAccessKeyEnvVar": "AWS_SECRET_ACCESS_KEY"
          },
          "SyncIntervalSeconds": 1,
          "SnapshotIntervalMinutes": 60,
          "RetentionDays": 30,
          "MetricsPort": 9090,
          "HealthCheckEnabled": true,
          "HealthCheckMaxLagSeconds": 30
        }
      }
    }
  }
}
```

### Credentials live in environment variables, not in appsettings

The options object holds the **names** of the env vars Litestream reads at runtime — never the secret values themselves. This keeps the appsettings file safe to commit. Supply the actual credentials via systemd `EnvironmentFile=`, Docker secrets, AWS Secrets Manager, or whatever your platform uses. The validator logs a warning at startup if a referenced env var is unset, but does not fail — secrets often arrive after process start through indirection.

Supported replica targets: `S3` (incl. MinIO / Backblaze B2 / Wasabi via `EndpointUrl` + `ForcePathStyle`), `AzureBlob`, `GoogleCloudStorage`, `Sftp`, `LocalDisk` (intended for tests and development).

### Health check

When the toggle is on, a `litestream` health check tagged `polar-sql` + `polar-litestream` pings the Litestream metrics endpoint (`http://localhost:{MetricsPort}/metrics`) and parses `litestream_replica_lag_seconds`. Surface in your `/health` endpoint:

- `Healthy` — metrics reachable, max lag at or below `HealthCheckMaxLagSeconds`
- `Degraded` — metrics reachable, max lag above threshold
- `Unhealthy` — metrics endpoint unreachable (Litestream is likely not running)

### CLI helpers (scaffolded for v1.2.x.+1)

`LitestreamCliCommands` defines two verbs whose dispatch is deferred to the next release:

- `polar-mt litestream init` — generates `litestream.yml` from the resolved options and the SQLite database directory.
- `polar-mt litestream verify` — restores every replica to a temp directory and runs `PRAGMA integrity_check` on each.

Stage C ships the class skeletons with documented signatures; full implementations land in v1.2.x.+1.

### Running Litestream alongside your app

Litestream is an external process. Install it on each host that holds SQLite files (see [litestream.io install docs](https://litestream.io/install/)) and run it as a sidecar:

```sh
# Generate the config from your appsettings + database directory (after v1.2.x.+1):
polar-mt litestream init --output /etc/litestream/litestream.yml

# Run the daemon (systemd unit / Windows service / Docker sidecar):
litestream replicate -config /etc/litestream/litestream.yml
```

The application process and the Litestream process share the database directory via the filesystem — neither calls the other directly. WAL journal mode (enabled by default in `UseSqlite(...)`) is required for Litestream to stream changes safely.

### Auto-regeneration on tenant changes (Option D)

The Stage C model above (one `litestream.yml` regenerated by hand and a Litestream process restarted on demand) is the out-of-the-box experience. For hosts that prefer a **hands-off** model, the package ships an opt-in `IHostedService` that watches the database directory and keeps `litestream.yml` in sync automatically. It uses a `FileSystemWatcher` to observe `*.db` Created/Deleted events, debounces bursts, regenerates the YAML, writes it atomically (temp file + rename), and signals the Litestream process to reload its config via `SIGHUP` on POSIX hosts.

Opt in by setting:

```json
{
  "PolarSharp": {
    "MultiTenant": {
      "Sqlite": {
        "Litestream": {
          "UseLitestream": true,
          "AutoRegenerateOnTenantChange": true,
          "ConfigOutputPath": "/etc/litestream.yml",
          "LitestreamPidFilePath": "/var/run/litestream.pid",
          "AutoRegenerateDebounceWindow": "00:00:02"
        }
      }
    }
  }
}
```

- **`ConfigOutputPath`** — where the generator writes the YAML. Defaults to `/etc/litestream.yml` (matches Litestream's default config-file lookup). The host process must be able to write the file; on Linux this usually means either running the host with sufficient permissions or relocating the output path to a directory owned by the service account.
- **`LitestreamPidFilePath`** — where Litestream writes its PID file. Defaults to `/var/run/litestream.pid`. After regeneration, the hosted service reads the PID and calls `kill(pid, SIGHUP)` via a direct `libc` P/Invoke (AOT-safe, no extra package dependency). Set Litestream up to write its PID file by running it with `--pidfile /var/run/litestream.pid` or the equivalent in your systemd unit.
- **`AutoRegenerateDebounceWindow`** — collapses bursts of file events into a single regeneration. The default 2 seconds is long enough to absorb the multiple events a bulk-onboarding script will fire when it creates several `.db` files in quick succession, but short enough that a single tenant addition is reflected promptly.

**Windows limitation.** POSIX `SIGHUP` has no native Windows equivalent. On Windows hosts the auto-regenerator still writes the new YAML to `ConfigOutputPath`, but logs a Warning instead of signalling the Litestream process — operators must restart Litestream manually for the change to take effect. Most production Litestream deployments run on Linux, where the SIGHUP path works out of the box.

### Tenant lifecycle integration (Stage C.4)

Beyond `.db` file Created/Deleted events, the auto-regenerator subscribes to MediatR `TenantStatusChangedNotification` events published by `PolarSharp.MultiTenant.Lifecycle.ITenantStatusService`. Status changes drive the same regeneration pipeline as file events, with the per-tenant inclusion/exclusion state held in a shared `LitestreamRegenCoordinator` singleton:

- **Suspended / Inactive / Deleted** — the tenant's `.db` file is **excluded** from the regenerated YAML. The file stays on disk (so business data is preserved), but Litestream stops pushing new WAL changes on the next config reload. A YAML comment (`# tenant {id} excluded from replication (suspended/inactive/deleted)`) is emitted in the would-be location so an operator inspecting the generated file sees the omission explicitly.
- **Active (reactivation)** — the tenant's `.db` file is **re-included** in the regenerated YAML, and replication resumes on the next config reload.

**Snapshot retention.** Per the locked design decision, existing cloud snapshots for an excluded tenant are **preserved** on the replica target — they remain subject to the configured `RetentionDays`. Excluding a tenant only stops new WAL changes from being pushed; nothing is deleted from S3 / Azure / GCS / SFTP / local-disk as a side effect of suspension. Reactivating a tenant within the retention window therefore restores from the last full snapshot rather than starting from zero.

**Restart-after-suspension behavior.** When the host process starts, the auto-regenerator queries the registered `IMultiTenantStore<PolarTenantInfo>` for tenants whose `Status` is not `Active` and seeds the exclusion set before the initial sync. This ensures that the very first YAML written after a host restart correctly excludes any tenants that were already suspended when the host went down — there is no window where a suspended tenant would briefly resume replication on restart.

**No-op when disabled.** The `LitestreamTenantLifecycleHandler` is registered unconditionally as part of `AddPolarSqliteLitestream(...)`, but checks `UseLitestream` and `AutoRegenerateOnTenantChange` on every notification and exits early when either is `false`. Hosts that have Litestream disabled pay only the cost of the MediatR dispatch.

## License

MIT.
