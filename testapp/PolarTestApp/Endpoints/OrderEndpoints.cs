using PolarSharp;

namespace PolarTestApp.Endpoints;

internal static class OrderEndpoints
{
    internal static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/orders").WithTags("Orders");

        group.MapGet("/", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Orders.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListOrders")
        .WithSummary("List orders from Polar sandbox.");

        group.MapGet("/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Orders[id].GetAsync(cancellationToken: ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetOrder")
        .WithSummary("Get a single order by ID.");

        return app;
    }
}
