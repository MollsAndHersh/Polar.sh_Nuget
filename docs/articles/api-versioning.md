# API Versioning

PolarSharp pins your integration to a specific Polar API schema version so that Polar's continuous evolution doesn't silently break your host app.

## Two independent version dimensions

| Dimension | Setting | Controls | Changes |
|---|---|---|---|
| URL path | `BasePath` (default `/v1`) | Which major API version prefix is used in all URLs | Every 2–3 years |
| Schema date | `ApiVersion` (ISO date) | The `Polar-Version` HTTP header sent on every request | Continuously |

## `ApiVersion` header

Every outbound request includes `Polar-Version: {date}`. Polar's servers use this to apply the schema contract that was in effect on that date — field additions after that date are invisible to your client; deprecated fields remain available until the pinned date's support window expires.

```json
{
  "PolarSharp": {
    "ApiVersion": "2025-01-15"
  }
}
```

When `ApiVersion` is `null` (the default), PolarSharp sends the version the SDK was generated against: `PolarClient.GeneratedAgainstVersion`.

## Version mismatch detection

At startup, PolarSharp compares your `ApiVersion` to `PolarClient.GeneratedAgainstVersion`:

| Condition | `ApiVersionStrictness=Warn` | `Strict` | `Off` |
|---|---|---|---|
| Match | `Information` log | (same) | silent |
| Config newer than SDK | `Warning` — SDK may miss new fields | Startup exception | silent |
| Config older than SDK | `Warning` — Polar may use older schema | Startup exception | silent |

Example warning:
```
[WRN] PolarSharp: Configured ApiVersion '2025-04-01' is newer than the SDK's bundled
      version '2025-01-15'. New endpoints or fields added after '2025-01-15' are not
      exposed by this SDK version.
```

## `ApiVersionStrictness` values

```json
{
  "PolarSharp": {
    "ApiVersionStrictness": "Warn"
  }
}
```

| Value | Behaviour |
|---|---|
| `"Warn"` (default) | Log a `Warning` on mismatch; continue normally |
| `"Strict"` | Fail startup with `PolarConfigurationException` on any mismatch |
| `"Off"` | No check at all |

Use `"Strict"` in regulated production environments to guarantee no undocumented schema drift.

## Forward compatibility

STJ's default behaviour ignores unknown JSON properties, so Polar adding new response fields doesn't break existing consumers. Fields removed by Polar surface as `null` or default values — the host app's null-handling decides the outcome.

## Hot-reload

`ApiVersion` is read via `IOptionsMonitor<PolarOptions>`, so updating `appsettings.json` takes effect on the next outbound request without an app restart.

## Kiota regeneration

When Polar releases a new schema version:

```bash
kiota update -o src/PolarSharp/Generated
# updates kiota.lock, regenerates Generated/, and updates PolarApiMetadata.GeneratedAgainstVersion
```

Commit the regenerated files and the updated `PolarApiMetadata.GeneratedAgainstVersion` constant together.
