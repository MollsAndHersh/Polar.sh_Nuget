# Test vs. Live Mode

PolarSharp uses a `Mode` setting (not a raw URL) as the primary developer-facing switch between Polar's sandbox and production environments.

## Mode values

| `Mode` | Polar API | Behaviour |
|--------|-----------|-----------|
| `"Test"` (default) | `https://sandbox-api.polar.sh/v1` | No real charges. Use for all development and CI. |
| `"Live"` | `https://api.polar.sh/v1` | **Real transactions.** Use only in production. |
| `"Custom"` | `CustomBaseUrl` | Self-hosted Polar or a local proxy. |

```json
{
  "PolarSharp": {
    "Mode": "Test",
    "AccessToken": "tok_sandbox_xxx"
  }
}
```

## Startup mode banner

PolarSharp emits a startup log entry so you always know which mode is active:

**Test mode (`Information`):**
```
[INF] PolarSharp is running in TEST/SANDBOX mode.
      API: https://sandbox-api.polar.sh/v1
      No real transactions will be processed. Safe for development and testing.
```

**Live mode (`Warning`):**
```
[WRN] ════════════════════════════════════════════════════════════════
[WRN] PolarSharp is running in LIVE/PRODUCTION mode.
[WRN] API: https://api.polar.sh/v1
[WRN] ALL TRANSACTIONS WILL BE PROCESSED AS REAL PAYMENTS.
[WRN] Ensure this is intentional before serving real traffic.
[WRN] ════════════════════════════════════════════════════════════════
```

## Token-prefix sanity check

Polar sandbox tokens begin with `tok_sandbox_` and production tokens begin with `tok_live_`. PolarSharp verifies the prefix at startup and emits a `Warning` if there is a mismatch:

```
[WRN] PolarSharp: Mode is 'Live' but AccessToken appears to be a sandbox token
      (prefix 'tok_sandbox_'). Verify your configuration before processing real payments.
```

This catches the most common deployment mistake: a staging token accidentally deployed to production.

## Switching via environment variable

Use ASP.NET Core environment-specific configuration to keep test tokens out of production:

```json
// appsettings.json (checked in)
{
  "PolarSharp": {
    "Mode": "Test",
    "AccessToken": ""
  }
}
```

```json
// appsettings.Production.json (or Azure Key Vault / AWS Secrets Manager)
{
  "PolarSharp": {
    "Mode": "Live",
    "AccessToken": "tok_live_xxx"
  }
}
```

## Health check verification

`GET /health` (tagged `"polar"`) confirms the configured mode is reachable. A `Healthy` response verifies that PolarSharp can connect to the configured Polar endpoint and authenticate successfully.
