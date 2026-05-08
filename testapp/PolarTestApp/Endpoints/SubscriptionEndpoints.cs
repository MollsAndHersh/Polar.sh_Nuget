using PolarSharp;

namespace PolarTestApp.Endpoints;

internal static class SubscriptionEndpoints
{
    internal static WebApplication MapSubscriptionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/subscriptions").WithTags("Subscriptions");

        group.MapGet("/", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Subscriptions.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListSubscriptions")
        .WithSummary("List subscriptions from Polar sandbox.");

        group.MapGet("/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Subscriptions[id].GetAsync(cancellationToken: ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetSubscription")
        .WithSummary("Get a single subscription by ID.");

        return app;
    }
}
