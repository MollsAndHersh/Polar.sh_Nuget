using PolarSharp;

namespace PolarTestApp.Endpoints;

internal static class CheckoutEndpoints
{
    internal static WebApplication MapCheckoutEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/checkouts").WithTags("Checkouts");

        group.MapGet("/", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Checkouts.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListCheckouts")
        .WithSummary("List checkout sessions from Polar sandbox.");

        group.MapGet("/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Checkouts[id].GetAsync(cancellationToken: ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetCheckout")
        .WithSummary("Get a single checkout session by ID.");

        return app;
    }
}
