using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PolarSharp.Webhooks;

namespace PolarSharp.Benchmarks;

/// <summary>
/// Measures webhook HMAC-SHA256 signature verification throughput and allocation
/// profile under a simulated burst of 1 000 concurrent webhook deliveries.
/// Target: P99 &lt; 2 ms for a 50 KB payload; zero GC-pressure allocations after warmup.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class WebhookVerificationBenchmarks
{
    private WebhookValidator _validator    = null!;
    private string           _webhookId   = null!;
    private string           _timestamp   = null!;
    private byte[]           _bodyBytes   = null!;
    private string           _validSig    = null!;
    private const string     SecretB64    = "dGVzdC1zZWNyZXQta2V5LWZvci1iZW5jaG1hcmtpbmc=";  // base64(test-secret-key-for-benchmarking)

    [GlobalSetup]
    public void Setup()
    {
        var opts = new PolarWebhookOptions
        {
            Secrets           = [$"whsec_{SecretB64}"],
            Path              = "/hooks/polar",
            ToleranceSeconds  = 300,
        };
        var monitor  = new FixedOptionsMonitor<PolarWebhookOptions>(opts);
        _validator   = new WebhookValidator(monitor, NullLogger<WebhookValidator>.Instance);

        _webhookId  = "wh_bench_" + Guid.NewGuid().ToString("N");
        _timestamp  = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        _bodyBytes  = Encoding.UTF8.GetBytes(
            """{"type":"order.created","data":{"id":"ord_bench","amount":2999,"currency":"USD"}}""");

        // Compute a valid signature for use in the happy-path benchmarks
        var secretBytes   = Convert.FromBase64String(SecretB64);
        var signedContent = Encoding.UTF8.GetBytes($"{_webhookId}.{_timestamp}.");
        var payload       = new byte[signedContent.Length + _bodyBytes.Length];
        signedContent.CopyTo(payload, 0);
        _bodyBytes.CopyTo(payload, signedContent.Length);
        using var hmac    = new HMACSHA256(secretBytes);
        _validSig         = "v1," + Convert.ToBase64String(hmac.ComputeHash(payload));
    }

    /// <summary>
    /// Single-threaded verification with a valid signature — steady-state hot path.
    /// </summary>
    [Benchmark(Baseline = true)]
    public bool Verify_Valid_SingleThread() =>
        _validator.Verify(_webhookId, _timestamp, _validSig, _bodyBytes).IsSuccess;

    /// <summary>
    /// Burst of 1 000 concurrent verifications — validates throughput and
    /// that <c>ArrayPool&lt;byte&gt;</c> prevents GC pressure under load.
    /// </summary>
    [Benchmark]
    public void Burst_1000_Webhooks()
    {
        var tasks = Enumerable.Range(0, 1_000).Select(_ => Task.Run(() =>
            _validator.Verify(_webhookId, _timestamp, _validSig, _bodyBytes))).ToArray();
        Task.WaitAll(tasks);
    }

    /// <summary>
    /// Invalid signature path — validates that timing-uniform error handling
    /// computes the full HMAC even on bad input (no short-circuit).
    /// </summary>
    [Benchmark]
    public bool Verify_InvalidSignature() =>
        _validator.Verify(
            _webhookId, _timestamp,
            "v1,AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            _bodyBytes).IsSuccess;

    // Minimal IOptionsMonitor<T> stub — no need for a real DI container in benchmarks.
    private sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
