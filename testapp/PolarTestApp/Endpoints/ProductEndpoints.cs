using PolarSharp;

namespace PolarTestApp.Endpoints;

internal static class ProductEndpoints
{
    internal static WebApplication MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/products").WithTags("Products");

        group.MapGet("/", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Products.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListProducts")
        .WithSummary("List products from Polar sandbox.");

        group.MapGet("/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Products[id].GetAsync(cancellationToken: ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetProduct")
        .WithSummary("Get a single product by ID.");

        return app;
    }
}
