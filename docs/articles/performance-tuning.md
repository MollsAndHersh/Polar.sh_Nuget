# Performance Tuning

## Connection pool sizing

The default `MaxConnectionsPerServer = 100` suits most workloads. Tune based on your observed `polar.requests.inflight` gauge:

- If `inflight` regularly exceeds 80% of `MaxConnectionsPerServer`, increase the limit
- If `inflight` is consistently low, reduce to reclaim OS socket resources

```json
{
  "PolarSharp": {
    "Connection": {
      "MaxConnectionsPerServer": 200
    }
  }
}
```

## DNS rotation (`PooledConnectionLifetime`)

Cloud load balancers rotate backend IPs continuously. Without `PooledConnectionLifetime`, HTTP/1.1 connections stick to a stale IP for hours.

The default 15-minute lifetime forces TCP reconnection at that interval, ensuring requests reach the current backend:

```json
{
  "PolarSharp": {
    "Connection": {
      "PooledConnectionLifetimeMinutes": 15
    }
  }
}
```

Lower this value (e.g., 5 min) if Polar's LB rotates more aggressively. Do not set below 1 minute — frequent reconnections waste TLS handshake CPU.

## HTTP/2 multiplexing

`EnableHttp2: true` (default) allows one TCP connection to carry hundreds of concurrent `HttpClient` requests as independent streams. For burst traffic, this reduces connection pool usage by 10× compared to HTTP/1.1.

`EnableMultipleHttp2Connections: true` (default) allows additional TCP connections if the single HTTP/2 connection is saturated — combines multiplexing and parallelism.

## HTTP/3 (QUIC) — opt-in

HTTP/3 over QUIC eliminates TCP head-of-line blocking and reduces connection setup time. Enable experimentally:

```json
{
  "PolarSharp": {
    "Connection": {
      "EnableHttp3": true
    }
  }
}
```

Measure before committing — QUIC benefits depend on network conditions and Polar's server-side support.

## Hedging for latency-sensitive reads

P99 latency to Polar can be 10× P50 (tail latency). Hedging sends a duplicate `GET` request after a short delay and uses whichever response arrives first, cutting P99 by 60–80% at the cost of ~5% extra request volume:

```json
{
  "PolarSharp": {
    "Resilience": {
      "HedgeAfterMs": 200,
      "HedgeMaxAttempts": 2
    }
  }
}
```

Hedging is applied **only to `GET` and `HEAD` requests** — never to mutating verbs.

## Per-tenant bulkhead

Without `PolarSharp.MultiTenant`, all requests share one connection pool and one circuit breaker. With per-tenant isolation, each tenant gets its own resources:

- One tenant's circuit-breaker state never affects another tenant's requests
- One tenant's high throughput doesn't starve others' connection slots

## `polar.requests.inflight` gauge

Monitor this `UpDownCounter<long>` (tagged by `polar.tenant_id` and `polar.resource`) to detect saturation before it becomes a problem:

```
polar.requests.inflight{polar.tenant_id="acme", polar.resource="orders"} = 87
```

If this approaches `MaxConnectionsPerServer`, requests will start queuing behind the pool and latency will increase. Alert at 80% of the configured limit.

## Channel capacity tuning

Toast notification channel (`IPolarToastChannel`) uses `BoundedChannelFullMode.DropOldest`. Tune `ChannelCapacity` based on your UI's read speed:

- Blazor Server with persistent circuit: 50–100 (reads in real time)
- SignalR broadcast with occasional disconnections: 200–500
- SSE with frequent client reconnections: 500

Monitor `polar.channel.depth{name="toast"}` and alert above 80% of capacity.

## Startup JIT warmup

The first Polar API call in a JIT-compiled app is slow because hot paths haven't been compiled yet. Enable optional warmup:

```json
{
  "PolarSharp": {
    "WarmupOnStartup": true
  }
}
```

Adds ~100ms to startup; first user-facing request feels as fast as steady state. No-op in Native AOT builds.

## Native AOT

Native AOT (`PublishAot=true`) eliminates JIT startup latency entirely. PolarSharp is fully AOT-compatible — all code paths are reflection-free. CI verifies this with zero-warning AOT publish on every PR.

## BenchmarkDotNet results

Run benchmarks locally:

```bash
dotnet run --project tests/PolarSharp.Benchmarks -c Release
```

Key targets:

| Benchmark | Target |
|---|---|
| Per-call overhead vs raw `HttpClient` | < 5 ms (P50) |
| Webhook HMAC verification (50 KB) | < 2 ms (P99) |
| Multi-tenant client lookup (cache hit) | < 100 ns (P99) |
| 100 tenants × 100 parallel calls | Linear scaling |
