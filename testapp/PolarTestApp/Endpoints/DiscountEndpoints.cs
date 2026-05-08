using PolarSharp;

namespace PolarTestApp.Endpoints;

internal static class DiscountEndpoints
{
    internal static WebApplication MapDiscountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/discounts").WithTags("Discounts");

        group.MapGet("/", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Discounts.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListDiscounts")
        .WithSummary("List discounts from Polar sandbox.");

        group.MapGet("/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Discounts[id].GetAsync(cancellationToken: ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetDiscount")
        .WithSummary("Get a single discount by ID.");

        return app;
    }
}
