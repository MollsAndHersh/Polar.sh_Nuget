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
