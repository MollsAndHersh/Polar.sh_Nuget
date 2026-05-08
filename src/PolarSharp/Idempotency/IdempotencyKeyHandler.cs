using Microsoft.Extensions.Logging;

namespace PolarSharp.Idempotency;

/// <summary>
/// A <see cref="DelegatingHandler"/> that attaches an <c>X-Idempotency-Key</c> header to
/// all mutating HTTP requests to prevent duplicate operations on retry.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Auto-generates a GUID-based key for each new logical request if the caller does not supply one.</item>
///   <item>Reuses the same key across all retry attempts of the same logical request — never regenerates per attempt.</item>
///   <item>Only applied to <c>POST</c>, <c>PATCH</c>, <c>PUT</c>, and <c>DELETE</c> requests.</item>
///   <item><c>GET</c> and <c>HEAD</c> requests are never given an idempotency key.</item>
/// </list>
/// <para>
/// Keys are logged at <c>Debug</c> level (safe — not sensitive) for retry correlation.
/// </para>
/// </remarks>
internal sealed class IdempotencyKeyHandler(ILogger<IdempotencyKeyHandler> logger) : DelegatingHandler
{
    private const string IdempotencyKeyHeader = "X-Idempotency-Key";

    /// <summary>
    /// Attaches an idempotency key to mutating requests and forwards to the next handler.
    /// </summary>
    /// <param name="request">The outgoing HTTP request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The HTTP response from the next handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsMutatingMethod(request.Method))
            return base.SendAsync(request, ct);

        if (!request.Headers.Contains(IdempotencyKeyHeader))
        {
            var key = IdempotencyKey.NewKey();
            request.Headers.Add(IdempotencyKeyHeader, key.Value);
            logger.LogDebug("Attaching idempotency key {IdempotencyKey} to {Method} {Uri}",
                key.Value, request.Method, request.RequestUri?.PathAndQuery);
        }

        return base.SendAsync(request, ct);
    }

    private static bool IsMutatingMethod(HttpMethod method) =>
        method == HttpMethod.Post
        || method == HttpMethod.Patch
        || method == HttpMethod.Put
        || method == HttpMethod.Delete;
}
