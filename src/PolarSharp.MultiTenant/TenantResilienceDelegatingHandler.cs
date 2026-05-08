using Polly;

namespace PolarSharp.MultiTenant;

/// <summary>
/// A <see cref="DelegatingHandler"/> that executes every outbound HTTP request through a
/// per-tenant <see cref="ResiliencePipeline{T}"/> to provide bulkhead isolation between tenants.
/// </summary>
/// <remarks>
/// Each tenant gets its own instance of this handler backed by its own
/// <see cref="ResiliencePipeline{T}"/>, guaranteeing that one tenant's circuit-breaker state,
/// retry budget, and rate limit do not affect any other tenant.
/// </remarks>
internal sealed class TenantResilienceDelegatingHandler(
    ResiliencePipeline<HttpResponseMessage> pipeline) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return pipeline.ExecuteAsync(
            static (state, ct) => new ValueTask<HttpResponseMessage>(
                state.self.InnerSendAsync(state.req, ct)),
            (self: this, req: request),
            cancellationToken).AsTask();
    }

    private Task<HttpResponseMessage> InnerSendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => base.SendAsync(request, cancellationToken);
}
