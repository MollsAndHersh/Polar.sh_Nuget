# Security

## Webhook endpoint hardening checklist

PolarSharp applies the following defenses automatically when `AddPolarWebhooks()` is registered:

| Defense | Automatic? | Config |
|---|---|---|
| HMAC-SHA256 signature verification | ✅ | — |
| Timestamp replay protection (±5 min) | ✅ | `ToleranceSeconds` |
| Constant-time signature comparison | ✅ | — |
| Payload size cap (1 MB) | ✅ | `MaxPayloadBytes` |
| Rate limiting (300 req/min per IP) | ✅ | `RateLimitPermitLimit` / `EnableRateLimiting` |
| HTTPS enforcement (returns 400 not redirect) | ✅ | `RequireHttps` |
| Content-Type enforcement (application/json only) | ✅ | — |
| JSON deserialization hardening (MaxDepth=32, no comments/trailing commas) | ✅ | — |
| Timing-uniform error responses (all failures: same body, same delay) | ✅ | — |
| Webhook path obscurity warning (warns on default path) | ✅ | — |
| IP allowlisting | opt-in | `AllowedSourceIpRanges`, `EnableIpAllowlist` |
| Anomaly detection (spike in HMAC failures) | ✅ | — |

## Webhook path obscurity

The default path `/hooks/polar` is predictable. For production, use a randomized path segment:

```json
{
  "PolarSharp": {
    "Webhooks": {
      "Path": "/hooks/polar/a3f7b2c9-4d1e-48fa-9c6b-2e0d5f8a1b3c"
    }
  }
}
```

PolarSharp warns at startup when the default path is in use and suggests a deterministic GUID derived from your access token.

## IP allowlisting

Opt-in — requires keeping Polar's sender IP ranges up to date:

```json
{
  "PolarSharp": {
    "Webhooks": {
      "EnableIpAllowlist": true,
      "AllowedSourceIpRanges": ["34.x.x.x/16", "35.x.x.x/16"]
    }
  }
}
```

> Check [Polar's documentation](https://docs.polar.sh) for the current IP ranges. Behind a load balancer, set `UseForwardedForHeader: true` and configure `ForwardedHeadersMiddleware` appropriately.

## Token rotation (zero-downtime)

`BearerTokenHandler` uses `IOptionsMonitor<PolarOptions>` so the access token is hot-reloadable:

1. Update `PolarSharp:AccessToken` in your secret store (Azure Key Vault, AWS Secrets Manager, etc.)
2. The configuration provider propagates the change to `IOptionsMonitor`
3. The next outbound request uses the new token — **no app restart required**

The token-prefix sanity check re-runs on hot-reload and emits a `Warning` if the new token's prefix doesn't match the configured `Mode`.

## Zero-downtime webhook secret rotation

See [Webhooks — zero-downtime secret rotation](webhooks.md#zero-downtime-secret-rotation).

## Anti-SSRF for `CustomBaseUrl`

When `Mode = "Custom"`, PolarSharp validates at startup that `CustomBaseUrl` is not targeting internal infrastructure:

- RFC 1918 addresses (`10.x`, `172.16–31.x`, `192.168.x`) are blocked
- Metadata endpoint (`169.254.169.254`) is blocked
- Loopback (`127.x`, `::1`) is blocked
- `AllowAutoRedirect = false` on `HttpClient` prevents redirect-based SSRF

## TLS hardening

All outbound connections from PolarSharp enforce:

- **Minimum TLS 1.2** — TLS 1.0/1.1 explicitly disabled
- **Certificate revocation checking** — `CheckCertificateRevocationList = true`
- **No automatic redirects** — `AllowAutoRedirect = false`

## Anomaly detection

PolarSharp exposes `polar.webhooks.suspicious_activity` (`UpDownCounter<long>` gauge) via `IMeterFactory`. Set an alert on this metric:

- Value `1` = elevated verification failure rate from a single IP (≥10 failures in 60 s)
- Value `0` = normal

Alert on `polar.webhooks.suspicious_activity = 1` → investigate source IP (SHA-256 hashed in logs for privacy correlation).

## Incident response

When `polar.webhooks.suspicious_activity = 1`:

1. Check `polar.webhooks.rejected_invalid_signature` counter for rate
2. Enable IP allowlisting (`EnableIpAllowlist: true`) as an immediate defence
3. Rotate your webhook secret via the multi-secret rotation procedure
4. Check Polar's dashboard for any unauthorized webhook endpoint registrations

## PII in logs

PolarSharp enables `PolarPiiRedactor` by default in production (`Logging:RedactPii: true`):

- Customer emails → `j***@example.com`
- Customer names → initials only (`J.S.`)
- Error detail fields → truncated to 100 characters
- Remote IPs → SHA-256 hashed (correlatable, not raw)

Disable in development environments for full debug visibility:

```json
{
  "PolarSharp": {
    "Logging": {
      "RedactPii": false
    }
  }
}
```

## GDPR compliance notes

- PolarSharp never stores customer data — it is a pure HTTP client
- Webhook payloads are processed in memory and not persisted by the library
- Structured logs with `PolarPiiRedactor` active satisfy GDPR Article 5 data minimization
- The host app owns retention policy for any data extracted from webhook payloads

## PCI DSS

PolarSharp never receives, stores, or logs cardholder data. Polar handles all card flows server-side. PolarSharp only exchanges Polar API tokens and webhook payloads — never raw card numbers.
